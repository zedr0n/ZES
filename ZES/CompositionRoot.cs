using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using SimpleInjector;
using SqlStreamStore;
using ZES.Infrastructure;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Projections;
using ZES.Infrastructure.Sagas;
using ZES.Infrastructure.Serialization;
using ZES.Infrastructure.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;
using ZES.Interfaces.Serialization;
using ZES.Logging;
using ILog = ZES.Interfaces.ILog;

namespace ZES
{
    /// <inheritdoc />
    public sealed class CompositionRoot : ICompositionRoot
    {
        // private readonly bool _isMemoryStore = true;

        /// <summary>
        /// Register the services with container
        /// </summary>
        /// <param name="container"><see cref="SimpleInjector"/> container</param>
        public void ComposeApplication(Container container)
        {
            container.Options.RegisterParameterConventions(new List<IParameterConvention>
            {
                new LogNameConvention(),
                new UtcNowConvention()
            });
            container.Register<IBus, Bus>(Lifestyle.Singleton);
            
            container.RegisterConditional(
                typeof(IStreamStore),
                GetStore(container),
                c => 
                     c.Consumer.ImplementationType.GetGenericArguments().Contains(typeof(IAggregate)) ||
                     c.Consumer.ImplementationType.GetInterfaces().Contains(typeof(IBranchManager)) || 
                     c.Consumer.ImplementationType == typeof(CommandLog));
            
            container.RegisterConditional(
                typeof(IStreamStore),
                GetStore(container),
                c => c.Consumer.ImplementationType.GetGenericArguments().Contains(typeof(ISaga))); 
            
            container.Register(typeof(ISerializer<>), typeof(Serializer<>), Lifestyle.Singleton);
            container.Register(typeof(IEventStore<>), typeof(SqlEventStore<>), Lifestyle.Singleton);
            
            container.Register<ITimeline, Timeline>(Lifestyle.Singleton);
            container.Register<IMessageQueue, MessageQueue>(Lifestyle.Singleton);
            container.Register<ICommandLog, CommandLog>(Lifestyle.Singleton);
            container.Register(typeof(ILogger), () => LogManager.GetLogger(typeof(Logging.NLog).Name), Lifestyle.Singleton); 

            container.Register<ILog, Logging.NLog>(Lifestyle.Singleton);
            container.Register<IErrorLog, ErrorLog>(Lifestyle.Singleton);

            container.Register(typeof(IStreamLocator<>), typeof(StreamLocator<>), Lifestyle.Singleton);    
            container.Register(typeof(IEsRepository<>), typeof(EsRepository<>), Lifestyle.Singleton);
            
            container.RegisterDecorator(
                typeof(ICommandHandler<>),
                typeof(CommandHandler<>),
                Lifestyle.Singleton);

            container.RegisterConditional(
                typeof(IQueryHandler<,>),
                typeof(DecoratorQueryHandler<,>),
                Lifestyle.Transient,
                c =>
                    c.Consumer == null ||
                    (!c.Consumer.ImplementationType.IsClosedTypeOf(typeof(HistoricalQueryHandler<,,>)) && 
                    !c.Consumer.ImplementationType.IsClosedTypeOf(typeof(DecoratorQueryHandler<,>))));
            
            container.Register<IBranchManager, BranchManager>(Lifestyle.Singleton);
            
            container.Register<StreamFlow.Builder>(Lifestyle.Singleton);
        }
        
        private Registration GetStore(Container container)
        {
            return Lifestyle.Singleton.CreateRegistration(() => new InMemoryStreamStore(), container);
        }
    }
}