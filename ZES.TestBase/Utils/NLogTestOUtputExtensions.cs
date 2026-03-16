using NLog;
using Xunit;

namespace ZES.TestBase.Utils
{
    /// <summary>
    /// Provides extension methods for integrating NLog with xUnit's ITestOutputHelper.
    /// </summary>
    public static class NLogTestOutputExtensions
    {
        /// <summary>
        /// Retrieves an instance of an NLog logger associated with the xUnit test output helper.
        /// </summary>
        /// <param name="testOutputHelper">
        /// The xUnit test output helper instance used to redirect log output.
        /// </param>
        /// <returns>
        /// An instance of <see cref="ILogger"/> for logging through NLog.
        /// </returns>
        public static ILogger GetNLogLogger(this ITestOutputHelper testOutputHelper)
        {
            return testOutputHelper.GetNLogLogger(string.Empty, true);
        }

        /// <summary>
        /// Retrieves an instance of an NLog logger associated with the xUnit test output helper.
        /// </summary>
        /// <param name="testOutputHelper">
        /// The xUnit test output helper instance used to redirect log output.
        /// </param>
        /// <param name="loggerName">
        /// The name of the logger to be used.
        /// </param>
        /// <param name="addNumericSuffix">
        /// A boolean flag indicating whether to append a numeric suffix to the logger name for uniqueness.
        /// </param>
        /// <returns>
        /// An instance of <see cref="ILogger"/> for logging through NLog.
        /// </returns>
        public static ILogger GetNLogLogger(
            this ITestOutputHelper testOutputHelper,
            string loggerName,
            bool addNumericSuffix = false)
        {
            return LogManager.GetLogger(TestOutputHelpers.AddTestOutputHelper(testOutputHelper, loggerName, addNumericSuffix));
        }

        /// <summary>
        /// Removes the xUnit test output helper associated with the specified logger.
        /// </summary>
        /// <param name="logger">
        /// The NLog logger instance whose associated test output helper is to be removed.
        /// </param>
        public static void RemoveTestOutputHelper(this ILogger logger)
        {
            TestOutputHelpers.RemoveTestOutputHelper(logger.Name);
        }
    }
}