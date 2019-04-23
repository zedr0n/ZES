using System;
using ZES.Interfaces;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class Timeline : ITimeline
    {
        private long _now;

        /// <inheritdoc />
        public string Id { get; set; } = BranchManager.Master;

        /// <inheritdoc />
        public long Now
        {
            get => Id == BranchManager.Master ? DateTime.UtcNow.Ticks : _now;
            set => _now = value;
        }
    }
}