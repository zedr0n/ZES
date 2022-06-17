using NodaTime;
using ZES.Interfaces.Clocks;

namespace ZES.Infrastructure.Clocks
{
    /// <inheritdoc />
    public class PhysicalClock : IPhysicalClock
    {
        /// <inheritdoc />
        public Instant GetCurrentInstant() => SystemClock.Instance.GetCurrentInstant();
    }
}