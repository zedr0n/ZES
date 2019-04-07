using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    public class HistoricalProjection<T,TState> : Projection<TState> where T : Projection<TState>
                                                                     where TState : new()
    {
        private long _timestamp;
        private readonly Subject<int> _complete = new Subject<int>();
        private int _build = 0;
        private readonly IObservable<bool> Complete;

        public HistoricalProjection(IEventStore<IAggregate> eventStore, ILog logger, IMessageQueue messageQueue, ITimeline timeline, T projection) 
            : base(eventStore, logger, messageQueue, timeline)
        {
            foreach (var h in projection.Handlers)
                Register(h.Key, (e,s) => e.Timestamp <= _timestamp ? h.Value(e,s) : s);
            
            Complete = Observable.Create(async (IObserver<bool> observer) =>
                {
                    _complete.Subscribe(b =>
                    {
                        if (b == 0)
                            observer.OnCompleted();
                    });
                });
        }

        protected override void Pause()
        {
            _build++;
            base.Pause();
        }

        protected override void Unpause()
        {
            _build--;
            _complete.OnNext(_build);
            base.Unpause();
        }

        public async Task Init(long timestamp)
        {
            _timestamp = timestamp;
            var obs = Complete.Publish();
            obs.Connect();
            Rebuild();
            if(_build > 0)
                await obs;
        }
    }
}