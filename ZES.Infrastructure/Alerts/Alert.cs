using ZES.Interfaces;

namespace ZES.Infrastructure.Alerts
{
    /// <inheritdoc />
    public class Alert : IAlert
    {
        /// <inheritdoc />
        public long? Timestamp { get; }
    }
}