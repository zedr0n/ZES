using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using SqlStreamStore.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;

namespace ZES.CrossCuttingConcerns
{
    public class MessageQueue : IMessageQueue
    {
        public IObservable<IEvent> Messages => _messages.AsObservable();
        private readonly Subject<IEvent> _messages = new Subject<IEvent>();
        public Task PublishAsync(IEvent e)
        {
            return Task.Run(() => _messages.OnNext(e));
        }
    }
}