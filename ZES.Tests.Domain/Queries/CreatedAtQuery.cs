using ZES.Infrastructure;

namespace ZES.Tests.Domain.Queries
{
    public class CreatedAtQuery : Query<long>
    {
        public CreatedAtQuery(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }
}