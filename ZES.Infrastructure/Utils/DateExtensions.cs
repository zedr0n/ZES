using System;
using System.Globalization;
using NodaTime;
using NodaTime.Text;
using PublicHoliday;
using ZES.Interfaces.Clocks;

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
        /// Convert string to time
        /// </summary>
        /// <param name="date">Input date</param>
        /// <returns>Parse result</returns>
        public static Time ToTime(this string date)
        {
            if (date == null)
                return default;
            var instant = date.ToInstant();
            if (!instant.Success)
                return default;
            return instant.Value.ToTime();
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
        
        /// <summary>
        /// Converts timestamp to date string 
        /// </summary>
        /// <param name="time">Timestamp</param>
        /// <returns>Date string</returns>
        public static string ToDateString(this Time time)
        {
            return $"{time.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'", CultureInfo.CurrentCulture)}({time.ToUnixTimeMilliseconds()})";
        }

        /// <summary>
        /// Converts instant to time instance
        /// </summary>
        /// <param name="instant">Instant to convert</param>
        /// <returns>Time instance</returns>
        public static Time ToTime(this Instant instant)
        {
            if (!Time.UseLogicalTime)
                return new InstantTime(instant);
            else
                return new LogicalTime(instant.ToUnixTimeTicks(), 0);
        }

        /// <summary>
        /// Determines if the given instant is less than the specified number of working days from another instant.
        /// </summary>
        /// <param name="instant">The base instant to compare.</param>
        /// <param name="otherInstant">The instant to calculate the working days difference from.</param>
        /// <param name="days">The number of working days to compare against. Defaults to 1.</param>
        /// <returns>True if the given instant is less than the specified number of working days from the other instant; otherwise, false.</returns>
        public static bool LessWorkingDays(this Instant instant, Instant otherInstant, int days = 1)
        {
            if(instant > otherInstant)
                return false;
            
            var uk = new UKBankHoliday();
            var date = instant.InZone(DateTimeZoneProviders.Tzdb["Europe/London"]).Date;
            var otherDate = otherInstant.InZone(DateTimeZoneProviders.Tzdb["Europe/London"]).Date;
            var minDate = otherDate;
            while (days > 0)
            {
                if(uk.IsWorkingDay(minDate.ToDateTimeUnspecified()))
                    days--;
                minDate = minDate.PlusDays(-1);
            }
            return date >= minDate;
        }
    }
}