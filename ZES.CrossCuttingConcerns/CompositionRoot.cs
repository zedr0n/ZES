using SimpleInjector;
using SqlStreamStore;
using ZES.Infrastructure;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Serialization;
using ZES.Infrastructure.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Serialization;

namespace ZES.CrossCuttingConcerns
{
    public class CompositionRoot : ICompositionRoot
    {
        public virtual void ComposeApplication(Container container)
        {
            container.Register<IBus,Bus>(Lifestyle.Singleton);
            
            container.Register<IStreamStore>(() => new InMemoryStreamStore(),Lifestyle.Singleton);
            
            container.Register<IEventSerializer, EventSerializer>(Lifestyle.Singleton);
            container.Register<IEventStore, SqlEventStore>(Lifestyle.Singleton);
            
            container.Register<ITimeline, Timeline>(Lifestyle.Singleton);

            container.Register<IStreamLocator, StreamLocator>(Lifestyle.Singleton);
            container.Register<IDomainRepository,DomainRepository>(Lifestyle.Singleton);
        }
    }
}