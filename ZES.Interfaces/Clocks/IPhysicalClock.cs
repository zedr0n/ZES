using NodaTime;

namespace ZES.Interfaces.Clocks
{
    /// <summary>
    /// Common interface for physical clocks
    /// </summary>
    public interface IPhysicalClock
    {
        /// <summary>
        /// Get current instant as seen by physical clock
        /// </summary>
        /// <returns>Current instant</returns>
        Instant GetCurrentInstant();
    }
}