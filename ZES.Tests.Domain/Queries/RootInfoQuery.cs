using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoQuery : Query<RootInfo>, ISingleQuery
    {
        public RootInfoQuery() { }
        public RootInfoQuery(string id)
        {
            Id = id;
        }

        public string Id { get; set; }
    }
}