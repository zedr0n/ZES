using System;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class StatsQuery : IQuery<long>
    {
        public Type Type => typeof(StatsQuery);
    }
}