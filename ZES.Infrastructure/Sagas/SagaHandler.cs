using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Sagas
{
    public class SagaHandler<TSaga> : ISagaHandler<TSaga>
        where TSaga : class, ISaga, new()

    {
        private readonly IDomainRepository _repository;
        private readonly ConcurrentDictionary<Type, Action<IEvent>> _handlers = new ConcurrentDictionary<Type, Action<IEvent>>();

        protected SagaHandler(IMessageQueue messageQueue, IDomainRepository repository)
        {
            _repository = repository;
            messageQueue.Messages.Subscribe(When);
        }

        protected void Register<TEvent>(Func<TEvent, string> idFunc) where TEvent : class, IEvent
        {
            async void Handler(IEvent e)
            {
                var saga = await _repository.GetOrAdd<TSaga>(idFunc(e as TEvent)); 
                saga.When(e);
                await _repository.Save(saga);
            }
            _handlers[typeof(TEvent)] = Handler;
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