using System.Threading.Tasks;
using Xunit;

namespace ZES.Tests
{
    public class IntegrationTests : ZesTest
    {
        public IntegrationTests(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }
        
        [Fact]
        public async Task CanReplayLog()
        {
            var result = await Replay("../../../Ad-hoc/CanReplayLog.json");
            Assert.True(result.Result);
        }
    }
}