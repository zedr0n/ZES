using System;
using Gridsum.DataflowEx;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class DataflowOptionsEx : DataflowOptions
    {
        /// <summary>
        /// Gets or sets timeout on input dispatch to children
        /// </summary>
        /// <value>
        /// Timeout on input dispatch to children
        /// </value>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(-1);
    }
}