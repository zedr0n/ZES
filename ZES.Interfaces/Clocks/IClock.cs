namespace ZES.Interfaces.Clocks
{
    /// <summary>
    /// Common clock interface 
    /// </summary>
    public interface IClock
    {
        /// <summary>
        /// Get current instant as seen by clock
        /// </summary>
        /// <returns>Current instant</returns>
        Time GetCurrentInstant();

        /// <summary>
        /// Update the clock for received event
        /// </summary>
        /// <param name="received">Received message clock</param>
        /// <returns>Current time</returns>
        Time Receive(Time received);
    }
}