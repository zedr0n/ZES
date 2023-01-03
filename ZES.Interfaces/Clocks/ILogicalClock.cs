namespace ZES.Interfaces.Clocks
{
    /// <summary>
    /// Common interface for logical ( hybrid ) clocks
    /// </summary>
    public interface ILogicalClock : IClock
    {
        /// <summary>
        /// Update the clock for send/local event
        /// </summary>
        void Tick();
    }
}