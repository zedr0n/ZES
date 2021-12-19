using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotChocolate.Execution.Instrumentation;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Targets;
using SimpleInjector;
using Xunit.Abstractions;
using ZES.GraphQL;
using ZES.Infrastructure;
using ZES.Infrastructure.Causality;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Tests.Domain.Sagas;
using ZES.Tests.Utils;

namespace ZES.Tests
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

        protected virtual Container CreateContainer(List<Action<Container>> registrations = null) 
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
                container.Register<IDiagnosticObserver, DiagnosticObserver>(Lifestyle.Singleton);

                container.Options.AllowOverridingRegistrations = true;
                container.Register(typeof(ILogger), () => _logger, Lifestyle.Singleton);
                
                // container.Register<IGraph, Graph>(Lifestyle.Singleton);
                container.Register<IGraph, NullGraph>(Lifestyle.Singleton);
                container.Options.AllowOverridingRegistrations = false;
                
                if (registrations == null)
                    registrations = new List<Action<Container>>();
                foreach (var reg in registrations)
                    reg(container);

                // container.Verify();
                root.Verify(container);
                return container;
            }
        }

        private static CompositionRoot CreateRoot() => new CompositionRoot();
    }
}