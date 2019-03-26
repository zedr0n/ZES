using System;
using System.Collections.Generic;
using System.Threading;
using NLog;
using NLog.Config;
using NLog.Targets;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.CrossCuttingConcerns;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Tests.TestDomain;

namespace ZES.Tests
{
    public class NLogger : ILog
    {
        private readonly ILogger _logger;

        public NLogger(ILogger logger)
        {
            _logger = logger;
        }
        
        
        public void Trace(object value)
        {
            _logger.Trace($"{value} ${Thread.CurrentThread.ManagedThreadId}$");
        }

        public void Debug(object value)
        {
            _logger.Debug($"{value} ${Thread.CurrentThread.ManagedThreadId}$");
        }
        
        public void Error(object value)
        {
            _logger.Error($"{value} ${Thread.CurrentThread.ManagedThreadId}$");
        }
        
        public void Fatal(object value)
        {
            _logger.Fatal($"{value} ${Thread.CurrentThread.ManagedThreadId}$");
        }
    }
    
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
            //var consoleTarget = new TestOutputTarget 
            var testTarget = new TestOutputTarget 
            {
                Name = "Test",
                Layout = @"${date:format=HH\:mm\:ss.ffff} ${level} ${message} ${exception}"
            }; 
            
            var consoleTarget = new ColoredConsoleTarget
            {
                Name = "Console",
                Layout = @"${date:format=HH\:mm\:ss.ffff} ${level} ${message} ${exception}"
            };
            //consoleTarget.
            config.AddTarget(consoleTarget);
            config.AddTarget(testTarget);
            
            config.AddRuleForAllLevels(consoleTarget); // all to console
            config.AddRuleForAllLevels(testTarget);
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
                container.Register<ICommandHandler<CreateRootCommand>,CreateRootHandler>(Lifestyle.Singleton);

                container.Register(() => _logger, Lifestyle.Singleton);
                container.Register<ILog, NLogger>(Lifestyle.Singleton);
                
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