using System;
using System.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;
using ZES.Infrastructure;
using ZES.Interfaces;

namespace ZES.Logging
{
    public static class TypeExtensions
    {
        public static string GetName(this Type type)
        {
            var genericArguments = type.GetGenericArguments();
            var str = type.Name.Split('`')[0];
            if (genericArguments.Length > 0)
                str = $"{str}<";
            foreach (var arg in genericArguments)
            {
                str = $"{str}{arg.GetName()}";
                if (arg != genericArguments.Last())
                    str = $"{str},";
            }

            if (genericArguments.Length > 0)
                str = $"{str}>";
            return str;
        }
    }
    
    public class NLogger : ILog
    {
        private readonly ILogger _logger;

        public static LoggingConfiguration Configure()
        {
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
            
            var consoleTarget = new ColoredConsoleTarget
            {
                Name = "Console",
                Layout = trace  
            };
            config.AddTarget(consoleTarget);

            //config.AddRuleForAllLevels(consoleTarget); // all to console
            LogManager.Configuration = config;
            
            return config;
        }
        
        public NLogger(ILogger logger)
        {
            _logger = logger;
        }

        public void Trace(object message)
        {
            _logger.Trace(" {msg}",message);
        }
        
        public void Trace(object message, object instance)
            //public void Trace(object message)
        {
            _logger.Trace("{dtype} {msg}",instance?.GetType().GetName(),message);
            //_logger.Trace($"[{instance?.GetType().Name??""}]{message}");
        }

        public void Debug(object message)
        {
            _logger.Debug("{msg}",message);
        }
        
        public void Error(object message, object instance = null)
        {
            _logger.Error("{dtype} {msg}",instance?.GetType().GetName() ?? "",message);
        }

        public void Error(Exception e, string message)
        {
            _logger.Error(e,message);
        }

        public void Fatal(object message, object instance = null)
        {
            _logger.Fatal("{dtype} {msg}",instance?.GetType().GetName() ?? "",message);
        }
    }
}