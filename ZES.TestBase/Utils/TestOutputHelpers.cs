using System.Linq;
using System.Threading;
using NLog;
using Xunit;

namespace ZES.TestBase.Utils 
{
    public static class TestOutputHelpers
    {
        private static int _loggerId;

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

        public static void RemoveTestOutputHelper(string loggerName)
        {
            foreach (TestOutputTarget testOutputTarget in LogManager.Configuration.AllTargets.OfType<TestOutputTarget>())
                testOutputTarget.Remove(loggerName);
        }
    }
}