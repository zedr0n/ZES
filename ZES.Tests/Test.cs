using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Targets;
using SimpleInjector;
using Xunit.Abstractions;
using ZES.GraphQL;
using ZES.Infrastructure;
using ZES.Tests.Utils;

namespace ZES.Tests
{
    public class Test : IDisposable
    {
        private readonly object _lock = new object();
        private readonly ILogger _logger;

        protected Test(ITestOutputHelper outputHelper)
        { 
            var config = Logging.NLog.Configure();
            var layout = config.AllTargets.OfType<TargetWithLayout>().First().Layout;

            // Step 2. Create targets
            var testTarget = new TestOutputTarget 
            {
                Name = "Test",
                Layout = layout 
            };  
            config.AddTarget(testTarget);

            if (Configuration.LogEnabled("GridSum"))
            {
                TestOutputHelpers.AddTestOutputHelper(outputHelper, "Gridsum.DataflowEx", false);
                TestOutputHelpers.AddTestOutputHelper(outputHelper, "Gridsum.DataflowEx.PerfMon", false);   
            }
            
            if (Configuration.LogEnabled("InMemoryStreamStore"))
                TestOutputHelpers.AddTestOutputHelper(outputHelper, "InMemoryStreamStore", false);   
            
            Logging.NLog.Enable(config);
            
            LogManager.Configuration = config;
            _logger = outputHelper.GetNLogLogger();
        }
        
        public void Dispose()
        {
            lock (_lock)
            {
                _logger.RemoveTestOutputHelper();
            }
        }
        
        protected virtual Container CreateContainer(List<Action<Container>> registrations = null) 
        {
            lock (_lock)
            {
                var container = new Container();
                container.Options.DefaultLifestyle = Lifestyle.Singleton;

                CreateRoot().ComposeApplication(container);
                container.Register<IGraphQlGenerator, GraphQlGenerator>(Lifestyle.Singleton);
                container.Register<IServiceCollection>(() => new ServiceCollection(), Lifestyle.Singleton);
                container.Register<ISchemaProvider, SchemaProvider>(Lifestyle.Singleton);

                container.Options.AllowOverridingRegistrations = true;
                container.Register(typeof(ILogger), () => _logger, Lifestyle.Singleton);
                container.Options.AllowOverridingRegistrations = false;
                
                if (registrations == null)
                    registrations = new List<Action<Container>>();
                foreach (var reg in registrations)
                    reg(container);

                container.Verify();
                return container;
            }
        }

        private static CompositionRoot CreateRoot() => new CompositionRoot();
    }
}