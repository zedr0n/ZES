using ZES.Interfaces.Clocks;

namespace ZES.Infrastructure.Clocks
{
    /// <summary>
    /// Instant time clock instance
    /// </summary>
    public class InstantClock : IClock
    {
        private readonly IPhysicalClock _physicalClock;

        /// <summary>
        /// Initializes a new instance of the <see cref="InstantClock"/> class.
        /// </summary>
        /// <param name="physicalClock">Physical clock instance</param>
        public InstantClock(IPhysicalClock physicalClock)
        {
            _physicalClock = physicalClock;
        }

        /// <inheritdoc />
        public Time GetCurrentInstant() => new InstantTime(_physicalClock.GetCurrentInstant());

        /// <inheritdoc />
        public Time Receive(Time received) => GetCurrentInstant();
    }
}