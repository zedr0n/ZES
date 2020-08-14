using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NLog;
using SimpleInjector;
using SqlStreamStore;
using ZES.Conventions;
using ZES.Infrastructure;
using ZES.Infrastructure.Branching;
using ZES.Infrastructure.Causality;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.EventStore;
using ZES.Infrastructure.Net;
using ZES.Infrastructure.Serialization;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.GraphQL;
using ZES.Interfaces.Net;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Serialization;
using ZES.Utils;
using BranchManager = ZES.Infrastructure.Branching.BranchManager;
using ILog = ZES.Interfaces.ILog;

namespace ZES
{
    /// <inheritdoc />
    public sealed class CompositionRoot : ICompositionRoot
    {
        /// <summary>
        /// Verify the container 
        /// </summary>
        /// <param name="container">Container</param>
        public void Verify(Container container)
        {
            var registrations = container.GetCurrentRegistrations();
            if (registrations.All(r => r.ServiceType != typeof(IEventDeserializer)))
                container.Collection.Register<IEventDeserializer>(new Type[] { });
            if (registrations.All(r => r.ServiceType != typeof(IEventSerializer)))
                container.Collection.Register<IEventSerializer>(new Type[] { });
            if (registrations.All(r => r.ServiceType != typeof(IGraphQlMutation)))
                container.Collection.Register<IGraphQlMutation>(new Type[] { });
            if (registrations.All(r => r.ServiceType != typeof(IGraphQlQuery)))
                container.Collection.Register<IGraphQlQuery>(new Type[] { });
            container.Verify();
        }
        
        /// <summary>
        /// Register the services with container
        /// </summary>
        /// <param name="container"><see cref="SimpleInjector"/> container</param>
        public void ComposeApplication(Container container)
        {
            container.Options.RegisterParameterConventions(new List<IParameterConvention>
            {
                new LogNameConvention(),
                new UtcNowConvention(),
            });
            container.Register<IBus, Bus>(Lifestyle.Singleton);
            container.Register<IEsRegistry, EsRegistry>(Lifestyle.Singleton);
            container.Register<IProjectionManager, ProjectionManager>(Lifestyle.Singleton);
            
            container.Register<IJSonConnector, JsonConnector>(Lifestyle.Singleton);
            container.Register<ICommandHandler<RequestJson>, JsonRequestHandler>(Lifestyle.Singleton);
            
            var store = GetStore(container);
            
            container.RegisterConditional(
                typeof(IStreamStore),
                store,
                c => 
                     c.Consumer.ImplementationType.GetGenericArguments().Contains(typeof(IAggregate)) ||
                     c.Consumer.ImplementationType.GetInterfaces().Contains(typeof(IBranchManager)) || 
                     c.Consumer.ImplementationType == typeof(CommandLog) ||
                     c.Consumer.ImplementationType == typeof(Graph))
                ;
            
            /* container.RegisterConditional(
                typeof(IStreamStore),
                GetStore(container),
                c => c.Consumer.Target.Parameter?.GetCustomAttribute(typeof(RemoteAttribute)) != null); */
            
            container.RegisterConditional(
                typeof(IStreamStore),
                store, 
                c => 
                    c.Consumer.ImplementationType.GetInterfaces().Contains(typeof(IRemote)) &&
                    c.Consumer.Target.Parameter?.GetCustomAttribute(typeof(RemoteAttribute)) == null); 
            
            container.Register(typeof(IRemote), typeof(NullRemote), Lifestyle.Singleton);
            
            container.RegisterConditional(
                typeof(IStreamStore),
                store, 
                c => c.Consumer.ImplementationType.GetGenericArguments().Contains(typeof(ISaga))); 
            
            container.Register<IEventSerializationRegistry, EventSerializationRegistry>(Lifestyle.Singleton);
            container.Register(typeof(ISerializer<>), typeof(Serializer<>), Lifestyle.Singleton);
            container.Register(typeof(IEventStore<>), typeof(SqlEventStore<>), Lifestyle.Singleton);
            
            container.Register<ITimeline, Timeline>(Lifestyle.Singleton);
            container.Register<IMessageQueue, MessageQueue>(Lifestyle.Singleton);
            container.Register<ICommandLog, CommandLog>(Lifestyle.Singleton);
            container.Register<ICommandRegistry, CommandRegistry>(Lifestyle.Singleton);

            container.Register<ILog, Logging.NLog>(Lifestyle.Singleton);
            container.Register(typeof(ILogger), () => LogManager.GetLogger(nameof(Logging.NLog)), Lifestyle.Singleton); 

            container.Register<IErrorLog, ErrorLog>(Lifestyle.Singleton);
            container.Register<IRecordLog, RecordLog>(Lifestyle.Singleton);

            container.Register(typeof(IStreamLocator), typeof(StreamLocator), Lifestyle.Singleton);    
            container.Register(typeof(IEsRepository<>), typeof(EsRepository<>), Lifestyle.Singleton);
            container.Register<IGraph, NullGraph>(Lifestyle.Singleton);
            container.Register<IStopWatch, StopWatch>(Lifestyle.Singleton);
            
            container.RegisterDecorator(
                typeof(ICommandHandler<>),
                typeof(CommandHandler<>),
                Lifestyle.Singleton);

            container.RegisterConditional(
                typeof(IQueryHandler<,>),
                typeof(QueryHandlerDecorator<,>),
                Lifestyle.Transient,
                c =>
                    c.Consumer == null ||
                    (!c.Consumer.ImplementationType.IsClosedTypeOf(typeof(HistoricalQueryHandler<,,>)) && 
                    !c.Consumer.ImplementationType.IsClosedTypeOf(typeof(QueryHandlerDecorator<,>))));

            container.Register<IBranchManager, BranchManager>(Lifestyle.Singleton);
            container.Register<IRetroactive, Retroactive>(Lifestyle.Singleton);
        }

        /// <summary>
        /// Uses SQL remote store destroying the contents by default
        /// </summary>
        /// <param name="container">SimpleInjector container</param>
        /// <param name="dropAll">Clear the remote store</param>
        public void RegisterRemoteStore(Container container, bool dropAll = true)
        {
            container.Options.AllowOverridingRegistrations = true;
            container.Register(typeof(IRemote), typeof(Remote), Lifestyle.Singleton); 
            container.Options.AllowOverridingRegistrations = false;
            
            container.RegisterConditional(
                typeof(IStreamStore),
                GetRemoteStore(container, dropAll),
                c => c.Consumer.Target.Parameter?.GetCustomAttribute(typeof(RemoteAttribute)) != null);
        }
        
        /// <summary>
        /// Uses in memory remote store optionally sharing the instance with base container 
        /// </summary>
        /// <param name="container">SimpleInjector container</param>
        /// <param name="baseContainer">Base container to share the remote store from</param>
        public void RegisterLocalStore(Container container, Container baseContainer = null )
        {
            container.Options.AllowOverridingRegistrations = true;
            container.Register(typeof(IRemote), typeof(Remote), Lifestyle.Singleton); 
            container.Options.AllowOverridingRegistrations = false;

            var store = GetStore(container);
            if (baseContainer != null)
            {
                store = Lifestyle.Singleton.CreateRegistration(
                    () => baseContainer.GetInstance<RemoteStreamStore>().Store, container);
            }

            container.RegisterConditional(
                typeof(IStreamStore),
                store, 
                c => c.Consumer.Target.Parameter?.GetCustomAttribute(typeof(RemoteAttribute)) != null);
        } 
        
        private Registration GetStore(Container container)
        {
            return Lifestyle.Singleton.CreateRegistration(() => new InMemoryStreamStore(), container);
        }

        private Registration GetRemoteStore(Container container, bool dropAll = true)
        {
            return Lifestyle.Singleton.CreateRegistration(
                () =>
                {
                    IStreamStore store = null;
                    if (!Configuration.UseMySql)
                        store = GetMsSqlStore(dropAll).Result;
                    else
                        store = GetMySqlStore(dropAll).Result;

                    return store;
                },
                container);
        }

        private async Task<IStreamStore> GetMsSqlStore(bool dropAll)
        {
            var store = new MsSqlStreamStoreV3(new MsSqlStreamStoreV3Settings(Configuration.MsSqlConnectionString));

            if (dropAll)
                await store.DropAll();
            if (dropAll || !(await store.CheckSchema()).IsMatch())
                store.CreateSchemaIfNotExists().Wait();

            return store;
        }

        private async Task<IStreamStore> GetMySqlStore(bool dropAll)
        {
            var store = new MySqlStreamStore(new MySqlStreamStoreSettings(Configuration.MySqlConnectionString));

            if (dropAll)
                store.DropAll().Wait();
            if (dropAll || !store.CheckSchema().Result.IsMatch)
                store.CreateSchemaIfNotExists().Wait();

            return store;
        }

        private class RemoteStreamStore
        {
            public RemoteStreamStore([Remote] IStreamStore store)
            {
                Store = store;
            }
            
            public IStreamStore Store { get; }
        }
    }
}