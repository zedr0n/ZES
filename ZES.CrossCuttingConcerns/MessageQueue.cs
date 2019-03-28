using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using SqlStreamStore.Infrastructure;
using ZES.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;

namespace ZES.CrossCuttingConcerns
{
    public class MessageQueue : IMessageQueue
    {
        private readonly ILog _log;
        private readonly ActionBlock<IEvent> _actionBlock;
        private readonly ActionBlock<string> _alertBlock; 
        
        public IObservable<IEvent> Messages => _messages.AsObservable();
        private readonly Subject<IEvent> _messages = new Subject<IEvent>();

        private readonly Subject<string> _alerts = new Subject<string>();
        public IObservable<string> Alerts => _alerts.AsObservable();
        public MessageQueue(ILog log)
        {
            _log = log;
            _actionBlock = new ActionBlock<IEvent>(e => 
                    _messages.OnNext(e),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 1
                });
            
            _alertBlock = new ActionBlock<string>(s => _alerts.OnNext(s),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 1
                });
        }

        public async Task Alert(string alert)
        {
            _log.Trace(alert,this);
            await _alertBlock.SendAsync(alert);
        }

        public async Task Event(IEvent e)
        {
            _log.Trace(e.EventType,this);
            await _actionBlock.SendAsync(e);
        }
    }
}