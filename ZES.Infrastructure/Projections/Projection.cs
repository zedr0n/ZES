using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using SqlStreamStore.Infrastructure;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    public class Projection<TState> : IProjection<TState> where TState : new()
    {
        protected readonly ILog Log;
        protected readonly ITimeline Timeline;
        private readonly IMessageQueue _messageQueue;


        private IDisposable _connection; 
        private IDisposable _subscription;
        private readonly BufferBlock<IStream> _bufferBlock;
        private readonly ActionBlock<IStream> _actionBlock;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ConcurrentDictionary<string,int> _streams = new ConcurrentDictionary<string, int>();
        internal readonly Dictionary<Type, Func<IEvent,TState,TState>> Handlers = new Dictionary<Type, Func<IEvent, TState, TState>>();
            
        private readonly Func<string, bool> _streamFilter = s => true;
        
        public IObservable<bool> Complete { get; }
        private bool _rebuilding;
        private readonly Subject<bool> _buildSubject = new Subject<bool>();
        
        public TState State { get; protected set; }

        protected Projection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue, ITimeline timeline)
        {
            _eventStore = eventStore;
            Log = log;
            _messageQueue = messageQueue;            
            Timeline = timeline;
            
            _actionBlock = new ActionBlock<IStream>(Update,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 8
                });
            _bufferBlock = new BufferBlock<IStream>();

            Complete = Observable.Create( async (IObserver<bool> observer) =>
            {
                if (!_rebuilding)
                {
                    observer.OnNext(true);
                    observer.OnCompleted(); 
                }

                _buildSubject.Subscribe(b => {
                    if (_rebuilding)
                        return;
                    observer.OnNext(true);
                    observer.OnCompleted();
                });
            });
            
            OnInit();
        }

        public virtual void OnInit()
        {
            Start();
        }

        public void Start(bool rebuild = true)
        {
            if(rebuild)
                Rebuild();
            _eventStore.Streams.Subscribe(async s => await Notify(s));
            _messageQueue.Alerts.OfType<InvalidateProjections>().Subscribe(s => Rebuild()); 
        }
        
        protected void Register<TEvent>(Func<TEvent,TState,TState> when) where TEvent : class
        {
            Handlers.Add(typeof(TEvent), (e,s) => when(e as TEvent,s)); 
        }

        protected void Register(Type tEvent, Func<IEvent,TState,TState> when)
        {
            Handlers.Add(tEvent, when);
        }

        protected void Register<TEvent>(Action<TEvent> action) where TEvent : class, IEvent
        {
            TState Handler(IEvent e, TState s)
            {
                action(e as TEvent);
                return State;
            }
            Handlers.Add(typeof(TEvent), Handler);
        }

        private async Task Notify(IStream stream)
        {
            if (!_streamFilter(stream.Key))
                return;
            
            var version = stream.Version; 
            var projectionVersion = _streams.GetOrAdd(stream.Key,-1);
            Log.Trace($"{stream.Key}@{projectionVersion}",this); 
            if (version > projectionVersion)
                await _bufferBlock.SendAsync(stream);
        }

        protected virtual void Pause()
        {
            Log.Trace("", this);
            _connection?.Dispose();
        }
        
        protected virtual void Unpause()
        {
            _rebuilding = false;
            Log.Trace("", this);
            _connection = _bufferBlock.LinkTo(_actionBlock);
            _subscription.Dispose();
        }
        
        protected async Task Rebuild()
        {
            bool StreamFilter(string s) => _streamFilter(s) && s.StartsWith(Timeline.Id);
            
            _rebuilding = true;
            _subscription?.Dispose();
            
            Pause();
            
            State = new TState();
            
            _subscription = _eventStore.Events.Where(e => StreamFilter(e.Stream))
                .Finally(Unpause) 
                .Subscribe(When);
            await Complete;
        }

        private async Task Update(IStream stream)
        {
            if(!_streams.TryGetValue(stream.Key, out var version))
                throw new InvalidOperationException("Stream not registered");
            
            Log.Trace($"{stream.Key}@{stream.Version}",this);     
            
            var streamEvents = (await _eventStore.ReadStream(stream, version+1)).ToList();
            
            foreach (var e in streamEvents)
                When(e);
        }

        private void When(IEvent e)
        {
            if (e == null)
                return;

            if (!Handlers.TryGetValue(e.GetType(), out var handler)) 
                return;

            //lock (State)
            //{
                State = handler(e,State);    
            //}
            _streams[e.Stream] = e.Version;
        }
    }
}