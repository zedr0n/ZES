using ZES.Infrastructure;
using ZES.Infrastructure.Domain;

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