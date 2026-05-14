using System.Collections.Generic;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class Query<T> : IQuery<T>
    {
        /// <inheritdoc />
        public string Timeline { get; set; } = string.Empty;

        /// <inheritdoc />
        public Time Timestamp { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<Time> AdditionalTimestamps { get; set; }
    }
}