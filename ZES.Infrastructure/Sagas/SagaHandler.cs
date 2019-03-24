using System;
using System.Collections.Concurrent;
using NLog;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Sagas
{
    public class SagaHandler<TSaga> : ISagaHandler<TSaga>
        where TSaga : class, ISaga, new()

    {
        private readonly ILogger _logger;
        private readonly IDomainRepository _repository;
        private readonly ConcurrentDictionary<Type, Action<IEvent>> _handlers = new ConcurrentDictionary<Type, Action<IEvent>>();

        protected SagaHandler(IMessageQueue messageQueue, IDomainRepository repository, ILogger logger)
        {
            _repository = repository;
            _logger = logger;
            messageQueue.Messages.Subscribe(When);
        }

        protected void Register<TEvent>(Func<TEvent, string> idFunc) where TEvent : class, IEvent
        {
            async void Handler(IEvent e)
            {
                var saga = await _repository.GetOrAdd<TSaga>(idFunc(e as TEvent)); 
                saga.When(e);
                _logger.Debug($"Sending {e.EventType} to {saga.GetType().Name}");
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