using ZES.Infrastructure;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoQuery : Query<RootInfo>
    {
        public RootInfoQuery() { }
        public RootInfoQuery(string id)
        {
            Id = id;
        }

        public string Id { get; set; }
    }
}