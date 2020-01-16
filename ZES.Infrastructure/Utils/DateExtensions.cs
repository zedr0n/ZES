using System;

namespace ZES.Infrastructure.Utils
{
    /// <summary>
    /// Date extensions
    /// </summary>
    public static class DateExtensions
    {
        /// <summary>
        /// Converts timestamp to date string 
        /// </summary>
        /// <param name="time">Unix timestamp</param>
        /// <returns>Date string</returns>
        public static string ToDateString(this long time)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(time).DateTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
        }
    }
}