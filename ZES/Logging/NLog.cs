using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.Targets;
using ZES.Infrastructure;
using ZES.Interfaces;
using ZES.Utils;

namespace ZES.Logging
{
    /// <inheritdoc />
    public class NLog : ILog
    {
        private static MemoryTarget _memory;

        private readonly ILogger _logger;
        private readonly Subject<string> _logs = new Subject<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="NLog"/> class.
        /// </summary>
        /// <param name="logger"><see cref="ILogger"/></param>
        /// <param name="stopWatch">Performance stopwatch</param>
        public NLog(ILogger logger, IStopWatch stopWatch)
        {
            _logger = logger;
            StopWatch = stopWatch;
            if (!Configuration.CommonLogEnabled())
                return;
            
            var props = new Common.Logging.Configuration.NameValueCollection
            {
                { "ConfigType", "INLINE" },
            };
            Common.Logging.LogManager.Adapter = new Common.Logging.NLog.NLogLoggerFactoryAdapter(props);
        }

        /// <inheritdoc />
        public IStopWatch StopWatch { get; }

        /// <inheritdoc />
        public IErrorLog Errors { get; set; }

        /// <inheritdoc />
        public IObservable<string> Logs => _logs.AsObservable(); 
        
        /// <inheritdoc />
        public IList<string> MemoryLogs => _memory.Logs;

        /// <summary>
        /// Enable log levels according to environment variables
        /// </summary>
        /// <param name="config">logging configuration</param>
        /// <param name="logEnabled">force enable logs</param>
        public static void Enable(LoggingConfiguration config, string logEnabled = default)
        {
            foreach (var target in config.AllTargets)
            {
                if (Environment.GetEnvironmentVariable("TRACE") == "1" || (logEnabled?.Contains("TRACE") ?? false))
                    config.AddRuleForOneLevel(LogLevel.Trace, target);
                if (Environment.GetEnvironmentVariable("DEBUG") == "1" || (logEnabled?.Contains("DEBUG") ?? false))
                    config.AddRuleForOneLevel(LogLevel.Debug, target);
                if (Environment.GetEnvironmentVariable("ERROR") == "1" || (logEnabled?.Contains("ERROR") ?? false))
                    config.AddRuleForOneLevel(LogLevel.Error, target);
                if (Environment.GetEnvironmentVariable("INFO") == "1" || (logEnabled?.Contains("INFO") ?? false))
                    config.AddRuleForOneLevel(LogLevel.Info, target);
                if (Environment.GetEnvironmentVariable("WARN") == "1" || (logEnabled?.Contains("WARN") ?? false))
                    config.AddRuleForOneLevel(LogLevel.Warn, target);
                if (Environment.GetEnvironmentVariable("FATAL") == "1" || (logEnabled?.Contains("FATAL") ?? false))
                    config.AddRuleForOneLevel(LogLevel.Fatal, target);
            }
        }
        
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
                Layout = trace, 
            };
            
            _memory = new MemoryTarget
            {
                Name = "Memory",
                Layout = trace,
            };
            
            config.AddTarget(consoleTarget);
            config.AddTarget(_memory);
            LogManager.Configuration = config;
            
            return config;
        }

        /// <inheritdoc />
        public void Trace(object message)
        {
            _logger.Trace(" {msg}", message);
            
            _logs.OnNext(_memory.Logs.LastOrDefault());
        }

        /// <inheritdoc />
        public void Trace(object message, object instance)
        {
            var type = instance is string ? instance : instance?.GetType().GetFriendlyName();
            _logger.Trace("{dtype} {msg}", type, message);
            
            _logs.OnNext(_memory.Logs.LastOrDefault());
        }

        /// <inheritdoc />
        public void Debug(object message, object instance = null, [CallerMemberName] string caller = "")
        {
            var type = instance is string ? instance : instance?.GetType().GetFriendlyName();
            if (!Configuration.LogEnabled(caller)) 
                return;
            _logger.Debug("{dtype} {msg}", type ?? string.Empty, message);
            _logs.OnNext(_memory.Logs.LastOrDefault());
        }

        /// <inheritdoc />
        public void Info(object message, object instance)
        {
            var type = instance is string ? instance : instance?.GetType().GetFriendlyName();
            _logger.Info("{dtype} {msg}", type ?? string.Empty, message);
            _logs.OnNext(_memory.Logs.LastOrDefault());
        }
        
        /// <inheritdoc />
        public void Warn(object message, object instance = null)
        {
            var type = instance is string ? instance : instance?.GetType().GetFriendlyName();
            _logger.Warn("{dtype} {msg}", type ?? string.Empty, message);
            _logs.OnNext(_memory.Logs.LastOrDefault());
        } 

        /// <inheritdoc />
        public void Error(object message, object instance = null)
        {
            var type = instance is string ? instance : instance?.GetType().GetFriendlyName();
            _logger.Error("{dtype} {msg}", type ?? string.Empty, message);
            _logs.OnNext(_memory.Logs.LastOrDefault());
        }

        /// <inheritdoc />
        public void Fatal(object message, object instance = null)
        {
            var type = instance is string ? instance : instance?.GetType().GetFriendlyName();
            _logger.Fatal("{dtype} {msg}", type ?? string.Empty, message);
            _logs.OnNext(_memory.Logs.LastOrDefault());
        }
    }
}