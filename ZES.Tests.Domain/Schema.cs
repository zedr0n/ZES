using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;

namespace ZES.Tests.Domain
{
    public static class Schema
    {
        public class Query
        {
            public CreatedAt CreatedAt(CreatedAtQuery query) => null;
            public Stats Stats(StatsQuery query) => null;
        }

        public class Mutation
        {
            public bool CreateRoot(CreateRoot command) => true;
        }
    }

}