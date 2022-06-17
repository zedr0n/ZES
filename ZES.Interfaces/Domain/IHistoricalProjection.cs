using NodaTime;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Historical projection
    /// </summary>
    public interface IHistoricalProjection
    {
        /// <summary>
        /// Gets or sets latest timestamp 
        /// </summary>
        Time Timestamp { get; set; }
    }

    /// <summary>
    /// Historical projection
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    public interface IHistoricalProjection<out TState> : IHistoricalProjection, IProjection<TState>
    {
    }
}