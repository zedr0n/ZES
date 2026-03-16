using System;
using System.Collections.Concurrent;
using NLog;
using NLog.Targets;
using Xunit;

namespace ZES.TestBase.Utils 
{
    /// <summary>
    /// A custom logging target for integrating NLog with xUnit's <see cref="ITestOutputHelper"/>.
    /// This target facilitates writing log messages to the xUnit test output, allowing better visibility
    /// of log data during test execution.
    /// </summary>
    [Target("TestOutput")]
    public class TestOutputTarget : TargetWithLayoutHeaderAndFooter
    {
        private readonly ConcurrentDictionary<string, ITestOutputHelper> _map = new ConcurrentDictionary<string, ITestOutputHelper>();

        /// <summary>
        /// Adds or associates an <see cref="ITestOutputHelper"/> instance with a specific logger name.
        /// </summary>
        /// <param name="testOutputHelper">
        /// The instance of <see cref="ITestOutputHelper"/> to associate with the logger.
        /// </param>
        /// <param name="loggerName">
        /// The name of the logger to associate with the <paramref name="testOutputHelper"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="loggerName"/> is null, empty, or consists only of white-space characters.
        /// </exception>
        public void Add(ITestOutputHelper testOutputHelper, string loggerName)
        {
            if (string.IsNullOrWhiteSpace(loggerName))
                throw new ArgumentNullException(nameof(loggerName));
            _map.TryAdd(loggerName, testOutputHelper);
        }

        /// <summary>
        /// Removes the association of a logger name with its corresponding <see cref="ITestOutputHelper"/> instance.
        /// </summary>
        /// <param name="loggerName">
        /// The name of the logger whose association with an <see cref="ITestOutputHelper"/> instance is to be removed.
        /// </param>
        /// <returns>
        /// A boolean value indicating whether the specified association was successfully removed.
        /// Returns <c>true</c> if the association was found and removed; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="loggerName"/> is null, empty, or consists only of white-space characters.
        /// </exception>
        public bool Remove(string loggerName)
        {
            if (string.IsNullOrWhiteSpace(loggerName))
                throw new ArgumentNullException(nameof(loggerName));
            return _map.TryRemove(loggerName, out _);
        }

        /// <summary>
        /// Writes a log event to the associated xUnit <see cref="ITestOutputHelper"/> instance if one is mapped to the logger name.
        /// </summary>
        /// <param name="logEvent">
        /// The details of the log event, including the logger name, log level, message, and other contextual information.
        /// </param>
        protected override void Write(LogEventInfo logEvent)
        {
            if (!_map.TryGetValue(logEvent.LoggerName!, out var testOutputHelper))
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