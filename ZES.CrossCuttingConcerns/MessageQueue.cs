using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using SqlStreamStore.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;

namespace ZES.CrossCuttingConcerns
{
    public class MessageQueue : IMessageQueue
    {
        private readonly ActionBlock<IEvent> _actionBlock;
        public IObservable<IEvent> Messages => _messages.AsObservable();
        private readonly Subject<IEvent> _messages = new Subject<IEvent>();

        public MessageQueue()
        {
            _actionBlock = new ActionBlock<IEvent>(e => 
                    _messages.OnNext(e),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 1
                });
        }

        public async Task PublishAsync(IEvent e)
        {
            await _actionBlock.SendAsync(e);
        }
    }
}