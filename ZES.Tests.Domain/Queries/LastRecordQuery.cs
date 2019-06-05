using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class LastRecordQuery : Query<LastRecord>
    {
        public LastRecordQuery(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }
}