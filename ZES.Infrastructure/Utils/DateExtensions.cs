using System;
using System.Globalization;
using NodaTime;
using NodaTime.Text;

namespace ZES.Infrastructure.Utils
{
    /// <summary>
    /// Date extensions
    /// </summary>
    public static class DateExtensions
    {
        /// <summary>
        /// Convert string to instant
        /// </summary>
        /// <param name="date">Input date</param>
        /// <returns>Parse result</returns>
        public static ParseResult<Instant> ToInstant(this string date)
        {
            if (string.IsNullOrEmpty(date))
                return ParseResult<Instant>.ForValue(default);

            return InstantPattern.ExtendedIso.Parse(date);
        }
        
        /// <summary>
        /// Converts timestamp to date string 
        /// </summary>
        /// <param name="time">Unix timestamp</param>
        /// <returns>Date string</returns>
        public static string ToDateString(this long time)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(time).DateTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
        }

        /// <summary>
        /// Converts timestamp to date string 
        /// </summary>
        /// <param name="time">Unix timestamp</param>
        /// <param name="format">Date format</param>
        /// <returns>Date string</returns>
        public static string ToDateString(this long time, string format)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(time).DateTime.ToString(format);
        }

        /// <summary>
        /// Converts timestamp to date string 
        /// </summary>
        /// <param name="time">Timestamp</param>
        /// <returns>Date string</returns>
        public static string ToDateString(this Instant time)
        {
            return $"{time.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'", CultureInfo.CurrentCulture)}({time.ToUnixTimeMilliseconds()})";
        }
    }
}