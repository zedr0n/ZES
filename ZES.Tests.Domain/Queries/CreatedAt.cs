using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class CreatedAt : IQuery<long>
    {
        public CreatedAt(string id)
        {
            this.id = id;
        }

        public string id { get; }
    }
}