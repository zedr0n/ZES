using System;
using System.Collections.Concurrent;
using NLog;
using NLog.Targets;
using Xunit.Abstractions;

namespace ZES.Tests 
{
    [Target("TestOutput")]
    public class TestOutputTarget : TargetWithLayoutHeaderAndFooter
    {
        private readonly ConcurrentDictionary<string, ITestOutputHelper> _map = new ConcurrentDictionary<string, ITestOutputHelper>();

        public void Add(ITestOutputHelper testOutputHelper, string loggerName)
        {
            if (string.IsNullOrWhiteSpace(loggerName))
                throw new ArgumentNullException(nameof(loggerName));
            _map.TryAdd(loggerName, testOutputHelper);
        }

        public bool Remove(string loggerName)
        {
            if (string.IsNullOrWhiteSpace(loggerName))
                throw new ArgumentNullException(nameof(loggerName));
            return _map.TryRemove(loggerName, out _);
        }

        protected override void Write(LogEventInfo logEvent)
        {
            if (!_map.TryGetValue(logEvent.LoggerName, out var testOutputHelper))
                return;
            try
            {
                var message = Layout.Render(logEvent);
                testOutputHelper.WriteLine(message);
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Debug("TestOutputTarget.Write - Exception: {0}", ex.Message);
            }
        }
    }
}