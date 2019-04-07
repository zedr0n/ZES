using System;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class CreatedAtQuery : IQuery<long>
    {
        public CreatedAtQuery(string id)
        {
            this.Id = id;
        }

        public string Id { get; }
        public Type Type => typeof(CreatedAtQuery);
    }
}