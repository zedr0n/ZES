using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class RootInfoQuery : SingleQuery<RootInfo>
    {
        public RootInfoQuery() { }
        public RootInfoQuery(string id)
        : base(id) { } 
    }
}