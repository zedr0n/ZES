using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using NLog;
using NLog.Config;
using NLog.Targets;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.CrossCuttingConcerns;
using ZES.Infrastructure;
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

            const string callSite = @"${callsite:className=True:skipFrames=1:includeNamespace=False:cleanNamesOfAnonymousDelegates=True:cleanNamesOfAsyncContinuations=True:fileName=False:includeSourcePath=False:methodName=True";
            const string trace = @"<${threadid:padding=2}> |${level:format=FirstCharacter}| ${date:format=HH\:mm\:ss.ff} " +
                                  //@" ${event-properties:dtype} " +
                                  callSite + @":when:when='${event-properties:dtype}' != ''}"  +
                                  //@"${callsite:className=True:skipFrames=1:includeNamespace=False:cleanNamesOfAnonymousDelegates=True:cleanNamesOfAsyncContinuations=True:fileName=False:includeSourcePath=False:methodName=True:}" +
                                  //@"( ${event-properties:dtype} )" +
                                  @"${literal:text=(:when:when='${event-properties:dtype}' != ''}" +@"${event-properties:msg}"+ @"${literal:text=):when:when='${event-properties:dtype}' != ''} "+
                                  @"${literal:text=[:when:when='${event-properties:dtype}' != ''}" + @"${event-properties:dtype}" + @"${literal:text=]:when:when='${event-properties:dtype}' != ''} " +
                                  @"${exception}";
            // Step 2. Create targets
            //var consoleTarget = new TestOutputTarget 
            var testTarget = new TestOutputTarget 
            {
                Name = "Test",
                Layout = trace 
            }; 
            
            var consoleTarget = new ColoredConsoleTarget
            {
                Name = "Console",
                Layout = trace  
            };
            //consoleTarget.
            config.AddTarget(consoleTarget);
            config.AddTarget(testTarget);
            
            //config.AddRuleForAllLevels(consoleTarget); // all to console
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

                //var logReg = Lifestyle.Singleton.CreateRegistration(() => _logger,container); 
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