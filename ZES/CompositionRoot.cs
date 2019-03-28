using System.Collections.Generic;
using System.Linq;
using NLog;
using SimpleInjector;
using SqlStreamStore;
using SqlStreamStore.Logging;
using ZES.Infrastructure;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Sagas;
using ZES.Infrastructure.Serialization;
using ZES.Infrastructure.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;
using ZES.Interfaces.Serialization;

namespace ZES
{
    public class CompositionRoot : ICompositionRoot
    {
        public virtual void ComposeApplication(Container container)
        {
            container.Options.RegisterParameterConventions(new List<IParameterConvention>
            {
                new LogNameConvention(),
                new UtcNowConvention()
            });
            container.Register<IBus,Bus>(Lifestyle.Singleton);
            
            //container.Register<IStreamStore>(() => new InMemoryStreamStore(),Lifestyle.Singleton);
            //container.Register(() => LogProvider.GetLogger("NLog"), Lifestyle.Singleton);
            /*container.RegisterConditional(typeof(IStreamStore),typeof(InMemoryStreamStore), Lifestyle.Singleton, 
                c => c.Consumer.ImplementationType.GetGenericArguments().Contains(typeof(IAggregate)));
            container.RegisterConditional(typeof(IStreamStore),typeof(InMemoryStreamStore), Lifestyle.Singleton, 
                c => c.Consumer.ImplementationType.GetGenericArguments().Contains(typeof(ISaga)));*/
            
            container.RegisterConditional(typeof(IStreamStore),Lifestyle.Singleton.CreateRegistration(() => new InMemoryStreamStore(), container),
                c => c.Consumer.ImplementationType.GetGenericArguments().Contains(typeof(IAggregate)));
            container.RegisterConditional(typeof(IStreamStore),Lifestyle.Singleton.CreateRegistration(() => new InMemoryStreamStore(), container),
                c => c.Consumer.ImplementationType.GetGenericArguments().Contains(typeof(ISaga))); 
            
            container.Register<IEventSerializer, EventSerializer>(Lifestyle.Singleton);
            container.Register(typeof(IEventStore<>), typeof(SqlEventStore<>),Lifestyle.Singleton);
            
            container.Register<ITimeline, Timeline>(Lifestyle.Singleton);
            container.Register<IMessageQueue,MessageQueue>(Lifestyle.Singleton);

            container.Register(typeof(IStreamLocator<>), typeof(StreamLocator<>),Lifestyle.Singleton);    
            container.Register<IDomainRepository,DomainRepository>(Lifestyle.Singleton);
            container.Register<ISagaRepository, SagaRepository>(Lifestyle.Singleton);
            
            container.RegisterDecorator(typeof(ICommandHandler<>),
                typeof(CommandRecorder<>),Lifestyle.Singleton,
                context => !context.AppliedDecorators.Any(d => d.IsClosedTypeOf(typeof(CommandRecorder<>)))); 

        }
    }
}