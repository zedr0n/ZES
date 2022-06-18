using System;
using NodaTime;

namespace ZES.Interfaces.Clocks
{
    /// <summary>
    /// Time record
    /// </summary>
    public abstract record Time : IComparable<Time>, IComparable, IFormattable
    {
        /// <summary>
        /// Gets a value indicating whether logical time is used instead of physical clocks
        /// </summary>
        public static bool UseLogicalTime => true;

        /// <summary>
        /// Gets default value for the time instance
        /// </summary>
        public static Time Default => UseLogicalTime ? new LogicalTime(0, 0) : new InstantTime(default(Instant));
        
        /// <summary>
        /// Gets the largest possible time instance
        /// </summary>
        public static Time MaxValue => UseLogicalTime ? LogicalTime.MaxValue : InstantTime.MaxValue;

        /// <summary>
        /// Gets the largest possible time instance
        /// </summary>
        public static Time MinValue => UseLogicalTime ? LogicalTime.MinValue : InstantTime.MinValue;

        /// <summary>
        /// Gets the time instance from extended iso string
        /// </summary>
        /// <param name="time">Extended iso string</param>
        /// <returns>Time instance</returns>
        public static Time FromExtendedIso(string time) =>
            UseLogicalTime ? LogicalTime.FromExtendedIso(time) : InstantTime.FromExtendedIso(time); 
        
        /// <summary>
        /// Convert the time instance to extended iso format
        /// </summary>
        /// <returns>Extended ISO timestamp</returns>
        public abstract string ToExtendedIso();

        /// <summary>
        /// Converts the time to physical instant 
        /// </summary>
        /// <returns>Corresponding instant</returns>
        public abstract Instant ToInstant();
        
        /// <summary>
        /// Get the time point just before the input time
        /// </summary>
        /// <returns>Time instance just before</returns>
        public abstract Time JustBefore();
        
        /// <summary>
        /// Convert to unix time milliseconds
        /// </summary>
        /// <returns>Unx time milliseconds</returns>
        public abstract long ToUnixTimeMilliseconds();
        
        /// <inheritdoc />
        public abstract int CompareTo(Time other);

        /// <inheritdoc />
        public abstract int CompareTo(object obj);

        /// <summary>
        /// Gets the duration between this and other 
        /// </summary>
        /// <param name="other">Other time instance</param>
        /// <returns>Other - current</returns>
        protected abstract Duration DurationTo(Time other);
        
        /// <summary>
        /// Add duration to time
        /// </summary>
        /// <param name="duration">Duration to add</param>
        /// <returns>Current + duration</returns>
        protected abstract Time AddDuration(Duration duration);

        /// <summary>
        /// Minus operator for time
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>b - a</returns>
        public static Duration operator -(Time a, Time b) => b.DurationTo(a);

        /// <summary>
        /// Plus operator for time
        /// </summary>
        /// <param name="a">Time instance</param>
        /// <param name="duration">Duration to add</param>
        /// <returns>Time such that duration between the two is equal to input duration</returns>
        public static Time operator +(Time a, Duration duration) => a.AddDuration(duration);
        
        /// <summary>
        /// Less operator overload for time 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less than b by lexicographical comparison</returns>
        public static bool operator <(Time a, Time b) => a.CompareTo(b) < 0;
        
        /// <summary>
        /// Less or equal operator overload for time 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less or equal than b by lexicographical comparison</returns>
        public static bool operator <=(Time a, Time b) => a.CompareTo(b) <= 0;

        /// <summary>
        /// Greater operator overload for time 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a greater than b by lexicographical comparison</returns>
        public static bool operator >(Time a, Time b) => a.CompareTo(b) > 0;
        
        /// <summary>
        /// Greater or equal operator overload for time 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a greater or equal than b by lexicographical comparison</returns>
        public static bool operator >=(Time a, Time b) => a.CompareTo(b) >= 0;

        /// <inheritdoc />
        public abstract string ToString(string format, IFormatProvider formatProvider);
    }
}