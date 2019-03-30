using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NLog.Targets;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Logging;

namespace ZES.Tests
{
    public class Test
    {
        private readonly object _lock = new object();
        private readonly ILogger _logger;

        protected void Dispose()
        {
            lock (_lock)
            {
                _logger.RemoveTestOutputHelper();
            }
        }
        
        protected Test(ITestOutputHelper outputHelper)
        { 
            var config = NLogger.Configure();
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
                if(Environment.GetEnvironmentVariable("TRACE") == "1")
                    config.AddRuleForOneLevel(LogLevel.Trace, target);
                if(Environment.GetEnvironmentVariable("DEBUG") == "1")
                    config.AddRuleForOneLevel(LogLevel.Debug, target);
            }
            
            LogManager.Configuration = config;
            _logger = outputHelper.GetNLogLogger();
        }
        
        private static CompositionRoot CreateRoot()
        {
            return new CompositionRoot();
        }

        protected Container CreateContainer( List<Action<Container>> registrations = null) 
        {
            lock (_lock)
            {
                var container = new Container();
                CreateRoot().ComposeApplication(container);
                Domain.Config.RegisterCommands(container);
                Domain.Config.RegisterQueries(container);
                Domain.Config.RegisterProjections(container);

                container.Options.AllowOverridingRegistrations = true;
                container.Register(typeof(ILogger),() => _logger,Lifestyle.Singleton);
                
                if(registrations == null)
                    registrations = new List<Action<Container>>();
                foreach(var reg in registrations)
                    reg(container);

                container.Verify();
                return container;
            }
        }
    }
}