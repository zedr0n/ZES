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
    }
}