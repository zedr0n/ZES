using System.Linq;
using System.Threading;
using NLog;
using Xunit;

namespace ZES.TestBase.Utils 
{
    /// <summary>
    /// A static helper class that provides functionality to integrate and manage
    /// test output logging with NLog in an xUnit testing environment.
    /// </summary>
    public static class TestOutputHelpers
    {
        private static int _loggerId;

        /// <summary>
        /// Adds a test output helper to the logging framework, allowing test output to be captured and associated
        /// with a specific logger name. Optionally appends a numeric suffix to the logger name for uniqueness.
        /// </summary>
        /// <param name="testOutputHelper">
        /// The instance of <see cref="ITestOutputHelper"/> used to capture test output.
        /// </param>
        /// <param name="loggerName">
        /// The name of the logger to associate with the test output helper. If null or whitespace, a default name "Test" is used.
        /// </param>
        /// <param name="addNumericSuffix">
        /// A boolean value indicating whether to append a numeric suffix to the logger name for uniqueness.
        /// </param>
        /// <returns>
        /// The name of the logger that was associated with the test output helper, including any appended numeric suffix if applicable.
        /// </returns>
        public static string AddTestOutputHelper(
            ITestOutputHelper testOutputHelper,
            string loggerName,
            bool addNumericSuffix)
        {
            var testOutputTargets = LogManager.Configuration.AllTargets.OfType<TestOutputTarget>();
            if (string.IsNullOrWhiteSpace(loggerName))
                loggerName = "Test";
            if (addNumericSuffix)
                loggerName += Interlocked.Increment(ref _loggerId).ToString();
            foreach (var testOutputTarget in testOutputTargets)
                testOutputTarget.Add(testOutputHelper, loggerName);
            return loggerName;
        }

        /// <summary>
        /// Removes the association between a logger and the test output helper, effectively stopping
        /// the capture of test output for the specified logger name.
        /// </summary>
        /// <param name="loggerName">
        /// The name of the logger whose test output helper association should be removed.
        /// If null or whitespace, an exception is thrown.
        /// </param>
        public static void RemoveTestOutputHelper(string loggerName)
        {
            foreach (TestOutputTarget testOutputTarget in LogManager.Configuration.AllTargets.OfType<TestOutputTarget>())
                testOutputTarget.Remove(loggerName);
        }
    }
}