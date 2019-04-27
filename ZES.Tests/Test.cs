using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NLog.Targets;
using SimpleInjector;
using Xunit.Abstractions;
using ZES.GraphQL;
using ZES.Logging;

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
            
            foreach (var target in config.AllTargets)
            {
                if (Environment.GetEnvironmentVariable("TRACE") == "1")
                    config.AddRuleForOneLevel(LogLevel.Trace, target);
                if (Environment.GetEnvironmentVariable("DEBUG") == "1")
                    config.AddRuleForOneLevel(LogLevel.Debug, target);
                if (Environment.GetEnvironmentVariable("ERROR") == "1")
                    config.AddRuleForOneLevel(LogLevel.Error, target);
                if (Environment.GetEnvironmentVariable("INFO") == "1")
                    config.AddRuleForOneLevel(LogLevel.Info, target);
                if (Environment.GetEnvironmentVariable("FATAL") == "1")
                    config.AddRuleForOneLevel(LogLevel.Fatal, target);
            }
            
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
                CreateRoot().ComposeApplication(container);
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