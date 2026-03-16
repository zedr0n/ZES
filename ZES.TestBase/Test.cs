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
using ZES.Interfaces.Branching;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Recording;
using ZES.Persistence.Redis;
using ZES.TestBase.Utils;
using ILogger = NLog.ILogger;

namespace ZES.TestBase
{
    /// <summary>
    /// Provides a base class for test implementations.
    /// </summary>
    /// <remarks>
    /// The <c>Test</c> class is an abstract helper designed for unit and integration tests.
    /// It supports configuration, logging utilities, and provides methods for replaying
    /// log files and setting up containers for dependency injection.
    /// </remarks>
    /// <example>
    /// This class should be extended by test classes to inherit common functionality for
    /// managing logs, setting up test environments, and handling replay scenarios.
    /// </example>
    public abstract class Test : IDisposable
    {
        private readonly ILogger _logger;
        private readonly object _lock = new object();

        /// <summary>
        /// Creates a new instance of the <see cref="Test"/> class. 
        /// </summary>
        /// <param name="outputHelper"></param>
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

        /// <summary>
        /// Indicates whether logging is enabled for the current context or feature.
        /// </summary>
        /// <remarks>
        /// The property is used to dynamically enable or disable logging features
        /// within the test framework or other components. This can be useful for
        /// controlling output verbosity or for selectively capturing logs during
        /// specific test scenarios.
        /// </remarks>
        protected virtual string LogEnabled => null;

        /// <summary>
        /// Gets the collection of configuration types required for the current test context.
        /// </summary>
        /// <remarks>
        /// The property is used to provide a set of types that represent configurations
        /// or modules required for initializing or customizing components during testing.
        /// These types are typically consumed by components such as the GraphQL replayer
        /// to configure specific behaviors or functionalities during test execution.
        /// </remarks>
        protected virtual IEnumerable<Type> Configs { get; } = null;

        /// <summary>
        /// Releases the resources used by the <see cref="Test"/> class.
        /// </summary>
        /// <remarks>
        /// This method ensures that any resources or dependencies, such as logging utilities,
        /// are properly disposed of and cleaned up after the test execution is completed.
        /// </remarks>
        public void Dispose()
        {
            lock (_lock)
            {
                _logger.RemoveTestOutputHelper();
            }
        }

        /// <summary>
        /// Replays a given log file using the configured GraphQL settings and logger.
        /// </summary>
        /// <param name="logFile">The path to the log file to be replayed.</param>
        /// <returns>A <see cref="ReplayResult"/> containing the details of the replay operation, including elapsed time, differences, and result status.</returns>
        protected async Task<ReplayResult> Replay(string logFile)
        {
            var player = new Replayer();
            lock (_lock)
                player.UseGraphQl(Configs, _logger);
            
            var result = await player.Replay(logFile);
            return result;
        }

        /// <summary>
        /// Creates and configures a new instance of the <see cref="Container"/>.
        /// </summary>
        /// <param name="registrations">A list of actions for configuring additional registrations within the container.</param>
        /// <param name="resetDb">A flag indicating whether the database should be reset during container initialization.</param>
        /// <param name="db">An integer specifying the database configuration, if applicable.</param>
        /// <returns>A fully configured instance of the <see cref="Container"/>.</returns>
        protected virtual Container CreateContainer(List<Action<Container>> registrations = null, bool resetDb = false,
            int db = 0)
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