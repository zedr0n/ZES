using System;
using NodaTime;
using NodaTime.Text;

#pragma warning disable SA1300

namespace ZES.Interfaces.Clocks
{
    /// <summary>
    /// Nodatime instant time
    /// </summary>
    /// <param name="instant"></param>
    public sealed record InstantTime(Instant instant) : Time
    {
        /// <summary>
        /// Gets the NodaTime instant
        /// </summary>
        public Instant instant { get; init; } = instant;

        /// <summary>
        /// Gets the largest possible time instance
        /// </summary>
        public new static InstantTime MaxValue => Instant.MaxValue; 

        /// <summary>
        /// Gets the smallest possible time instance
        /// </summary>
        public new static InstantTime MinValue => Instant.MinValue; 
        
        /// <summary>
        /// Gets the time instance from extended iso string
        /// </summary>
        /// <param name="time">Extended iso string</param>
        /// <returns>Time instance</returns>
        public new static InstantTime FromExtendedIso(string time)
        {
            var parseResult = InstantPattern.ExtendedIso.Parse(time);
            return parseResult.Success ? parseResult.Value : default(InstantTime);
        }
        
        /// <summary>
        /// Instant -> InstantTime operator 
        /// </summary>
        /// <param name="instant">Instant instance</param>
        /// <returns>InstantTime instance</returns>
        public static implicit operator InstantTime(Instant instant) => new InstantTime(instant);
        
        /// <summary>
        /// Less than operator 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less than b </returns> 
        public static bool operator <(InstantTime a, InstantTime b) => a.instant < b.instant;
        
        /// <summary>
        /// Less or equal than operator 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less than b </returns> 
        public static bool operator <=(InstantTime a, InstantTime b) => a.instant <= b.instant;

        /// <summary>
        /// Greater than operator 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a greter than b </returns> 
        public static bool operator >(InstantTime a, InstantTime b) => a.instant > b.instant;

        /// <summary>
        /// Greater or equal than operator 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less than b </returns> 
        public static bool operator >=(InstantTime a, InstantTime b) => a.instant >= b.instant;

        /// <inheritdoc />
        public override string ToExtendedIso() => InstantPattern.ExtendedIso.Format(instant);

        /// <inheritdoc />
        public override Instant ToInstant() => instant;

        /// <inheritdoc />
        public override Time JustBefore() => new InstantTime(instant - Duration.FromMilliseconds(1));

        /// <inheritdoc />
        protected override Duration DurationTo(Time other)
        {
            switch (other)
            {
                case null:
                    return Duration.Zero;
                case InstantTime instantTime:
                    return instantTime.instant - instant;
                default:
                    throw new InvalidCastException($"Object must be of type {this.GetType()}");
            }    
        }

        /// <inheritdoc />
        protected override Time AddDuration(Duration duration) => new InstantTime(instant + duration);
        
        /// <inheritdoc />
        public override long ToUnixTimeMilliseconds() => instant.ToUnixTimeMilliseconds();

        /// <inheritdoc />
        public override int CompareTo(Time other)
        {
            switch (other)
            {
                case null:
                    return 1;
                case InstantTime instantTime:
                    return instant.CompareTo(instantTime.instant);
                default:
                    throw new InvalidCastException($"Object must be of type {this.GetType()}");
            }
        }

        /// <inheritdoc />
        public override int CompareTo(object obj)
        {
            switch (obj)
            {
                case null:
                    return 1;
                case InstantTime time:
                    return CompareTo(time);
                default:
                    throw new InvalidCastException($"Object must be of type {this.GetType()}");
            }
        }

        /// <inheritdoc />
        public override string ToString(string format, IFormatProvider formatProvider) =>
            instant.ToString(format, formatProvider);
    }
}