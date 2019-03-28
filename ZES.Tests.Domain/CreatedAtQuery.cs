using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain
{
    public class CreatedAtQuery : IQuery<long>
    {
        public CreatedAtQuery(string id)
        {
            this.id = id;
        }

        public string id { get; }
    }
}