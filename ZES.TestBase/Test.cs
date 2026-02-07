using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Embedded;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Targets;
using SimpleInjector;
using Xunit;
using ZES.GraphQL;
using ZES.Infrastructure;
using ZES.Infrastructure.Causality;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Persistence.Redis;
using ZES.TestBase.Utils;
using ILogger = NLog.ILogger;

namespace ZES.TestBase
{
    public abstract class Test : IDisposable
    {
        private readonly ILogger _logger;
        private readonly object _lock = new object();

        protected Test(ITestOutputHelper outputHelper)
        { 
            var config = Logging.NLog.Configure();
            var layout = config.AllTargets.OfType<TargetWithLayout>().First().Layout;

            // Step 2. Create targets
            var testTarget = new TestOutputTarget 
            {
                Name = "Test",
                Layout = layout, 
            };  
            config.AddTarget(testTarget);

            if (Configuration.LogEnabled("GridSum"))
            {
                TestOutputHelpers.AddTestOutputHelper(outputHelper, "Gridsum.DataflowEx", false);
                TestOutputHelpers.AddTestOutputHelper(outputHelper, "Gridsum.DataflowEx.PerfMon", false);   
            }
            
            if (Configuration.LogEnabled("InMemoryStreamStore"))
                TestOutputHelpers.AddTestOutputHelper(outputHelper, "InMemoryStreamStore", false);   
            
            Logging.NLog.Enable(config, LogEnabled);
            
            LogManager.Configuration = config;
            _logger = outputHelper.GetNLogLogger();
        }

        protected virtual string LogEnabled => default;
        
        protected virtual IEnumerable<Type> Configs { get; } = null;

        public void Dispose()
        {
            lock (_lock)
            {
                _logger.RemoveTestOutputHelper();
            }
        }
        
        protected async Task<ReplayResult> Replay(string logFile)
        {
            var player = new Replayer();
            lock (_lock)
                player.UseGraphQl(Configs, _logger);
            
            var result = await player.Replay(logFile);
            return result;
        }
        
        protected virtual Container CreateContainer(List<Action<Container>> registrations = null, bool resetDb = false, int db = 0) 
        {
            lock (_lock)
            {
                var container = new Container();
                container.Options.DefaultLifestyle = Lifestyle.Singleton;

                var root = CreateRoot();
                root.ComposeApplication(container);
                container.Register<IGraphQlGenerator, GraphQlGenerator>(Lifestyle.Singleton);
                container.Register<IServiceCollection>(() => new ServiceCollection(), Lifestyle.Singleton);
                container.Register<ISchemaProvider, SchemaProvider>(Lifestyle.Singleton);

                container.Options.AllowOverridingRegistrations = true;
                container.Register(typeof(ILogger), () => _logger, Lifestyle.Singleton);
                
                // container.Register<IGraph, Graph>(Lifestyle.Singleton);
                container.Register<IGraph, NullGraph>(Lifestyle.Singleton);
                if (Configuration.EventStoreBackendType == EventStoreBackendType.EventStore && Configuration.UseEmbeddedTcpStore)
                    container.Register(GetEmbeddedConnection, Lifestyle.Singleton);
                container.Options.AllowOverridingRegistrations = false;
                
                if (registrations == null)
                    registrations = new List<Action<Container>>();
                foreach (var reg in registrations)
                    reg(container);

                // container.Verify();
                root.Verify(container);
                container.GetInstance<IConfigurationOverride>().ApplyOverride();
                if (resetDb)
                {
                    var eventStore = container.GetInstance<IEventStore<IAggregate>>();
                    eventStore.ResetDatabase().Wait();
                }

                if (db != 0 && Configuration.EventStoreBackendType == EventStoreBackendType.Redis)
                {
                    var connection = container.GetInstance<IRedisConnection>();
                    connection.Database = db;
                }
 
                return container;
            }
        }
        
        private static CompositionRoot CreateRoot() => new CompositionRoot();

        private IEventStoreConnection GetEmbeddedConnection()
        {
            var nodeBuilder = EmbeddedVNodeBuilder 
                .AsSingleNode()
                .OnDefaultEndpoints() 
                .StartStandardProjections()
                .DisableFirstLevelHttpAuthorization()
                .RunInMemory();

            var node = nodeBuilder.Build();
            node.Start();

            var scp = ConnectionSettings.Create()
                .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"));
            var connection = EmbeddedEventStoreConnection.Create(node, scp);
            connection.ConnectAsync().Wait();
            return connection;
        }
    }
}