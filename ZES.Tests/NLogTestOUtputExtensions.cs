using NLog;
using Xunit.Abstractions;
using ZES.Tests;

namespace Xunit
{
    public static class NLogTestOutputExtensions
    {
        public static ILogger GetNLogLogger(this ITestOutputHelper testOutputHelper)
        {
            return testOutputHelper.GetNLogLogger(string.Empty, true);
        }

        public static ILogger GetNLogLogger(
            this ITestOutputHelper testOutputHelper,
            string loggerName,
            bool addNumericSuffix = false)
        {
            return LogManager.GetLogger(TestOutputHelpers.AddTestOutputHelper(testOutputHelper, loggerName, addNumericSuffix));
        }

        public static void RemoveTestOutputHelper(this ILogger logger)
        {
            TestOutputHelpers.RemoveTestOutputHelper(logger.Name);
        }
    }
}