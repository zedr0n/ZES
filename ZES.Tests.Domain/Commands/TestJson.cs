using ZES.Interfaces.Net;

namespace ZES.Tests.Domain.Commands
{
    public class TestJson : IJsonResult
    {
        public string Id { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
    }
}