using System;
using ZES.Infrastructure;
using ZES.Interfaces.Domain;

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