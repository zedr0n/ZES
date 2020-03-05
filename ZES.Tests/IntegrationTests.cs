using Xunit;
using Xunit.Abstractions;

namespace ZES.Tests
{
    public class IntegrationTests : ZesTest
    {
        public IntegrationTests(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }
        
        [Fact]
        public async void CanReplayLog()
        {
            var result = await Replay("../../../Ad-hoc/CanReplayLog.json");
            Assert.True(result.Result);
        }
    }
}