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
            config.AddTarget(consoleTarget);
            
            config.AddRuleForAllLevels(consoleTarget); // all to console
            LogManager.Configuration = config;

            _logger = outputHelper.GetNLogLogger();
        }
        
        private static CompositionRoot CreateRoot()
        {
            return new CompositionRoot();
        }

        protected Container CreateContainer()
        {
            lock (_lock)
            {
                var container = new Container();
                CreateRoot().ComposeApplication(container);
                container.Register<ICommandHandler<CreateRootCommand>,CreateRootHandler>(Lifestyle.Singleton);

                container.Register<RootProjection>(Lifestyle.Singleton);
                container.Register<TestSaga>(Lifestyle.Singleton);
                container.Register<TestSagaHandler>(Lifestyle.Singleton);
                container.Register(typeof(IQueryHandler<,>), new[]
                {
                    typeof(CreatedAtHandler)
                }, Lifestyle.Singleton);
                
                container.Register(() => _logger, Lifestyle.Singleton);

                container.Verify();
                return container;
            }
        }
    }
}