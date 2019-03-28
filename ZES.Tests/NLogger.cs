using NLog;
using ZES.Infrastructure;

namespace ZES.Tests
{
    public class NLogger : ILog
    {
        private readonly ILogger _logger;

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
            _logger.Trace("{dtype} {msg}",instance?.GetType().Name,message);
            //_logger.Trace($"[{instance?.GetType().Name??""}]{message}");
        }

        public void Debug(object message)
        {
            _logger.Debug("{msg}",message);
        }
        
        public void Error(object message, object instance = null)
        {
            _logger.Error("{dtype} {msg}",instance?.GetType().Name ?? "",message);
        }
        
        public void Fatal(object message, object instance = null)
        {
            _logger.Fatal("{dtype} {msg}",instance?.GetType().Name ?? "",message);
        }
    }
}