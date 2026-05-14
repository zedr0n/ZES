using System.Collections.Generic;
using Newtonsoft.Json;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class Stats(int numberOfRoots) : IHistoricalState, IHistoricalResults<Stats>
    {
        public Stats() : this(0) { }
        public int NumberOfRoots => numberOfRoots;
        /// <inheritdoc/>
        [JsonIgnore]
        public Dictionary<Time, Stats> HistoricalResults { get; } = new();
    }
}