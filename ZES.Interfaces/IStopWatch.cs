using System.Collections.Generic;

namespace ZES.Interfaces
{
    /// <summary>
    /// Performance logger
    /// </summary>
    public interface IStopWatch
    {
        /// <summary>
        /// Gets all performance metrics
        /// </summary>
        Dictionary<string, long> Totals { get; }

        /// <summary>
        /// Gets or sets a value indicating whether performance monitoring is enabled
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Gets the elapsed milliseconds for metric
        /// </summary>
        /// <param name="descriptor">Performance descriptor</param>
        /// <returns>Number of elapsed milliseconds</returns>
        long Total(string descriptor);
        
        /// <summary>
        /// Start the stopwatch for metric
        /// </summary>
        /// <param name="descriptor">Performance descriptor</param>
        void Start(string descriptor);
        
        /// <summary>
        /// Stops the stopwatch for metric
        /// </summary>
        /// <param name="descriptor">Performance descriptor</param>
        void Stop(string descriptor);
    }
}