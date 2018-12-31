using System;
using System.Collections.Concurrent;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Sagas
{
    public class SagaHandler<TSaga> : ISagaHandler<TSaga>
        where TSaga : class, ISaga, new()

    {
        private readonly ConcurrentDictionary<Type, Action<IEvent>> _handlers = new ConcurrentDictionary<Type, Action<IEvent>>();

        protected SagaHandler(IMessageQueue messageQueue)
        {
            messageQueue.Messages.Subscribe(When);
        }

        protected void Register<TEvent>(Action<TEvent> action) where TEvent : class, IEvent
        {
            _handlers[typeof(TEvent)] = e => action(e as TEvent);
        }

        private void When(IEvent e)
        {
            if (_handlers.TryGetValue(e.GetType(), out var handler))
                handler(e);
        }
    }
}