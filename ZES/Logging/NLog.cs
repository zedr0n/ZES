using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Targets;
using ZES.Infrastructure;
using ZES.Interfaces;

namespace ZES.Logging
{
    /// <inheritdoc />
    public class NLog : ILog
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NLog"/> class.
        /// </summary>
        /// <param name="logger"><see cref="ILogger"/></param>
        public NLog(ILogger logger)
        {
            _logger = logger;
            var props = new Common.Logging.Configuration.NameValueCollection
            {
                { "ConfigType", "INLINE" }
            };
            Common.Logging.LogManager.Adapter = new Common.Logging.NLog.NLogLoggerFactoryAdapter(props);
        }
        
        /// <inheritdoc />
        public IErrorLog Errors { get; set; }

        /// <summary>
        /// Layout configuration for <see cref="NLog"/> 
        /// </summary>
        /// <returns><see cref="LoggingConfiguration"/></returns>
        public static LoggingConfiguration Configure()
        {
            var config = new LoggingConfiguration();

            const string callSite = @"${callsite:className=True:skipFrames=1:includeNamespace=False:cleanNamesOfAnonymousDelegates=True:cleanNamesOfAsyncContinuations=True:fileName=False:includeSourcePath=False:methodName=True";
            const string trace = @"<${threadid:padding=2}> |${level:format=FirstCharacter}| ${date:format=HH\:mm\:ss.ff} " +
                                  callSite + @":when:when='${event-properties:dtype}' != '' and level<=LogLevel.Info}" +
                                  @"${literal:text=(:when:when='${event-properties:dtype}' != ''}" + @"${event-properties:msg}" + @"${literal:text=):when:when='${event-properties:dtype}' != ''} " +
                                  @"${literal:text=[:when:when='${event-properties:dtype}' != ''}" + @"${event-properties:dtype}" + @"${literal:text=]:when:when='${event-properties:dtype}' != ''} " +
                                  @"${message:when='${event-properties:msg}' == ''}" + 
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

        /// <inheritdoc />
        public void Trace(object message)
        {
            _logger.Trace(" {msg}", message);
        }

        /// <inheritdoc />
        public void Trace(object message, object instance)
        {
            var type = instance is string ? instance : instance?.GetType().GetFriendlyName();
            _logger.Trace("{dtype} {msg}", type, message);
        }

        /// <inheritdoc />
        public void Debug(object message, object instance = null, [CallerMemberName] string caller = "")
        {
            var type = instance is string ? instance : instance?.GetType().GetFriendlyName();
            if (Configuration.LogEnabled(caller))
                _logger.Debug("{dtype} {msg}", type ?? string.Empty, message);
        }

        /// <inheritdoc />
        public void Info(object message)
        {
            _logger.Info("{msg}", message);
        }

        /// <inheritdoc />
        public void Error(object message, object instance = null)
        {
            var type = instance is string ? instance : instance?.GetType().GetFriendlyName();
            _logger.Error("{dtype} {msg}", type ?? string.Empty, message);
        }

        /// <inheritdoc />
        public void Fatal(object message, object instance = null)
        {
            var type = instance is string ? instance : instance?.GetType().GetFriendlyName();
            _logger.Fatal("{dtype} {msg}", type ?? string.Empty, message);
        }
    }
}