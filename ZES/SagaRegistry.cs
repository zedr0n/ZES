using System;
using System.Collections.Concurrent;
using System.Linq;
using SimpleInjector;
using ZES.Infrastructure.Sagas;
using ZES.Interfaces;
using ZES.Interfaces.Sagas;

namespace ZES
{
    public class SagaRegistry : ISagaRegistry
    {
        private readonly ConcurrentDictionary<Type, Func<IEvent,string> > _dictionary = new ConcurrentDictionary<Type, Func<IEvent, string>>();

        public void Register(Type saga, Func<IEvent, string> id)
        {
            _dictionary.TryAdd(saga, id);
        }

        public Func<IEvent, string> SagaId<TSaga>() => _dictionary[typeof(TSaga)];

        public SagaRegistry(Container c)
        {
            var sagas = c.GetCurrentRegistrations()
                .Where(r => r.Registration.ImplementationType.IsClosedTypeOf(typeof(SagaHandler<>)))
                .Select(r => r.Registration.ImplementationType.GenericTypeArguments[0]);

            foreach (var s in sagas)
            {
                var method = s.GetMethod(nameof(Saga.SagaId));
                if(method != null)
                    Register(s, e => (string) method.Invoke(null,new object[] {e}));
            }
        }
    }
}