using NLog;
using NLog.Config;
using NLog.Targets;
using ZES.Interfaces;

namespace ZES.Logging
{
    public class NLogger : ILog
    {
        private readonly ILogger _logger;
        
        public NLogger(ILogger logger)
        {
            _logger = logger;
        }

        public static LoggingConfiguration Configure()
        {
            var config = new LoggingConfiguration();

            const string callSite = @"${callsite:className=True:skipFrames=1:includeNamespace=False:cleanNamesOfAnonymousDelegates=True:cleanNamesOfAsyncContinuations=True:fileName=False:includeSourcePath=False:methodName=True";
            const string trace = @"<${threadid:padding=2}> |${level:format=FirstCharacter}| ${date:format=HH\:mm\:ss.ff} " +
                                  callSite + @":when:when='${event-properties:dtype}' != '' and level<=LogLevel.Info}" +
                                  @"${literal:text=(:when:when='${event-properties:dtype}' != ''}" + @"${event-properties:msg}" + @"${literal:text=):when:when='${event-properties:dtype}' != ''} " +
                                  @"${literal:text=[:when:when='${event-properties:dtype}' != ''}" + @"${event-properties:dtype}" + @"${literal:text=]:when:when='${event-properties:dtype}' != ''} " +
                                  @"${exception}";
            
            var consoleTarget = new ColoredConsoleTarget
            {
                Name = "Console",
                Layout = trace  
            };
            
            config.AddTarget(consoleTarget);
            LogManager.Configuration = config;
            
            return config;
        }

        public void Trace(object message)
        {
            _logger.Trace(" {msg}", message);
        }
        
        public void Trace(object message, object instance)
        {
            _logger.Trace("{dtype} {msg}", instance?.GetType().GetName(), message);
        }

        public void Debug(object message)
        {
            _logger.Debug("{msg}", message);
        }

        public void Info(object message)
        {
            _logger.Info("{msg}", message);
        }

        public void Error(object message, object instance = null)
        {
            var type = instance is string ? instance : instance?.GetType().GetName(); 
            _logger.Error("{dtype} {msg}", type ?? string.Empty, message);
        }

        public void Fatal(object message, object instance = null)
        {
            var type = instance is string ? instance : instance?.GetType().GetName(); 
            _logger.Fatal("{dtype} {msg}", type ?? string.Empty, message);
        }
    }
}