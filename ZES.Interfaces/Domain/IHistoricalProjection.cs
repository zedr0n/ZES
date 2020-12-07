using NodaTime;

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
        Instant Timestamp { get; set; }
    }

    /// <summary>
    /// Historical projection
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    public interface IHistoricalProjection<out TState> : IHistoricalProjection, IProjection<TState>
    {
    }
}