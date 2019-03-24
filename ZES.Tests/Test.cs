using System;
using System.Collections.Generic;
using NLog;
using NLog.Config;
using NLog.Targets;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.CrossCuttingConcerns;
using ZES.Interfaces.Domain;
using ZES.Tests.TestDomain;

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
            // Step 1. Create configuration object 
            var config = new LoggingConfiguration();

            // Step 2. Create targets
            var consoleTarget = new TestOutputTarget //new ColoredConsoleTarget("target1")
            {
                Name = "Test",
                Layout = @"${date:format=HH\:mm\:ss} ${level} ${message} ${exception}"
            };
            //consoleTarget.
            config.AddTarget(consoleTarget);
            
            config.AddRuleForAllLevels(consoleTarget); // all to console
            LogManager.Configuration = config;

            _logger = outputHelper.GetNLogLogger();
        }
        
        private static CompositionRoot CreateRoot()
        {
            return new CompositionRoot();
        }

        protected Container CreateContainer( List<Action<Container>> _registrations = null) 
        {
            lock (_lock)
            {
                var container = new Container();
                CreateRoot().ComposeApplication(container);
                container.Register<ICommandHandler<CreateRootCommand>,CreateRootHandler>(Lifestyle.Singleton);

                container.Register(() => _logger, Lifestyle.Singleton);
                
                if(_registrations == null)
                    _registrations = new List<Action<Container>>();
                foreach(var reg in _registrations)
                    reg(container);

                container.Verify();
                return container;
            }
        }
    }
}