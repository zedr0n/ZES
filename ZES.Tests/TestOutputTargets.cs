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
                throw new ArgumentNullException(nameof (loggerName));
            if (!this._map.TryAdd(loggerName, testOutputHelper))
                throw new ArgumentException("LoggerName already in use", nameof (loggerName));
        }

        public bool Remove(string loggerName)
        {
            if (string.IsNullOrWhiteSpace(loggerName))
                throw new ArgumentNullException(nameof (loggerName));
            ITestOutputHelper testOutputHelper;
            return this._map.TryRemove(loggerName, out testOutputHelper);
        }

        protected override void Write(LogEventInfo logEvent)
        {
            ITestOutputHelper testOutputHelper;
            if (!this._map.TryGetValue(logEvent.LoggerName, out testOutputHelper))
                return;
            try
            {
                string message = this.Layout.Render(logEvent);
                testOutputHelper.WriteLine(message);
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Debug("TestOutputTarget.Write - Exception: {0}", ex.Message);
            }
        }
    }
}