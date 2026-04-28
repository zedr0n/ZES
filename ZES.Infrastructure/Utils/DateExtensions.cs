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
        private static readonly UKBankHoliday Uk = new();
        
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
        /// Determines if the given instant falls on a working day in the UK.
        /// </summary>
        /// <param name="instant">The instant to check.</param>
        /// <returns>True if the instant falls on a working day; otherwise, false.</returns>
        public static bool IsWorkingDay(this Instant instant)
        {
            var date = instant.InZone(DateTimeZoneProviders.Tzdb["Europe/London"]).Date.ToDateTimeUnspecified();
            return Uk.IsWorkingDay(date);
        }

        /// <summary>
        /// Determines whether the number of working days between the specified instants is within the given limit.
        /// </summary>
        /// <param name="instant">The starting instant.</param>
        /// <param name="otherInstant">The comparison instant.</param>
        /// <param name="days">The maximum number of working days allowed between the two instants. Defaults to 0.</param>
        /// <returns>True if the number of working days is less than or equal to the specified limit; otherwise, false.</returns>
        /// <remarks>This method uses the UK bank holiday calendar to determine working days</remarks>
        public static bool IsWithinPriorWorkingDays(this Instant instant, Instant otherInstant, int days = 0)
        {
            var date = instant.InZone(DateTimeZoneProviders.Tzdb["Europe/London"]).Date.ToDateTimeUnspecified();
            var otherDate = otherInstant.InZone(DateTimeZoneProviders.Tzdb["Europe/London"]).Date
                .ToDateTimeUnspecified();
            if(date > otherDate)
                return false;
            
            var workingDays = Uk.BusinessDaysBetween(date, otherDate);
            return workingDays <= days;
        }

        /// <summary>
        /// Gets the end of the day for the specified instant in the specified timezone.
        /// </summary>
        /// <param name="instant">The input instant.</param>
        /// <param name="zoneId">The timezone ID. Defaults to "Europe/London".</param>
        /// <returns>The instant representing the end of the day in the specified timezone.</returns>
        public static Instant EndOfDay(this Instant instant, string zoneId = "Europe/London")
        {
            var localZone = DateTimeZoneProviders.Tzdb[zoneId];
            var localDate = instant.InZone(localZone);
            var endOfDay = localDate.Date.At(LocalTime.MaxValue).InZoneLeniently(localZone);
            return endOfDay.ToInstant();
        }

        /// <summary>
        /// Determines the start of the day for the given instant in the specified time zone.
        /// </summary>
        /// <param name="instant">The input instant.</param>
        /// <param name="zoneId">The time zone identifier. Defaults to "Europe/London".</param>
        /// <returns>The instant representing the start of the day in the specified time zone.</returns>
        public static Instant StartOfDay(this Instant instant, string zoneId = "Europe/London")
        {
            var localZone = DateTimeZoneProviders.Tzdb[zoneId];
            var localDate = instant.InZone(localZone);
            var startOfDay = localDate.Date.At(LocalTime.Midnight).InZoneLeniently(localZone);
            return startOfDay.ToInstant();
        }

        /// <summary>
        /// Determines the start of the specified date in a given time zone.
        /// </summary>
        /// <param name="time">The time for which the start of the date is determined.</param>
        /// <param name="zoneId">The time zone identifier (default is "Europe/London").</param>
        /// <returns>The start of the date represented as a <see cref="Time"/> object.</returns>
        public static Time StartOfDay(this Time time, string zoneId = "Europe/London") =>
            time.ToInstant().StartOfDay().ToTime();

        /// <summary>
        /// Gets the time of the previous day based on the provided time.
        /// </summary>
        /// <param name="time">The current time instance.</param>
        /// <returns>A <see cref="Time"/> instance representing the previous day.</returns>
        public static Time PreviousDay(this Time time) => time.ToInstant().PreviousDay().ToTime();
        
        /// <summary>
        /// Gets the end of the previous day for the specified instant in the specified timezone.
        /// </summary>
        /// <param name="instant">The input instant.</param>
        /// <param name="zoneId">The timezone ID. Defaults to "Europe/London".</param>
        /// <returns>The instant representing the end of the previous day in the specified timezone.</returns>
        public static Instant PreviousDay(this Instant instant, string zoneId = "Europe/London")
        {
            var localZone = DateTimeZoneProviders.Tzdb[zoneId];
            var localDate = instant.InZone(localZone);
            var previousDay = localDate.Plus(Duration.FromDays(-1));
            return previousDay.ToInstant().EndOfDay(zoneId);
        }

        /// <summary>
        /// Calculates the close of day for a given instant in a specified time zone.
        /// </summary>
        /// <param name="instant">The input instant.</param>
        /// <param name="zoneId">The time zone identifier. Defaults to "Europe/London".</param>
        /// <returns>The instant representing the close of day at 16:30 in the specified time zone.</returns>
        public static Instant CloseOfDay(this Instant instant, string zoneId = "Europe/London")
        {
            var localZone = DateTimeZoneProviders.Tzdb[zoneId];
            var localDate = instant.InZone(localZone);
            var closeOfDay = localDate.Date.At(new LocalTime(16, 30)).InZoneLeniently(localZone);
            return closeOfDay.ToInstant();
        }
    }
}