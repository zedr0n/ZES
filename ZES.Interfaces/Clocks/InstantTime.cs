using System;
using NodaTime;
using NodaTime.Text;

#pragma warning disable SA1300

namespace ZES.Interfaces.Clocks
{
    /// <summary>
    /// Nodatime instant time
    /// </summary>
    /// <param name="Instant">Instant value</param>
    public sealed record InstantTime(Instant Instant) : Time
    {
        /// <summary>
        /// Gets the NodaTime instant
        /// </summary>
        public Instant Instant { get; init; } = Instant;

        /// <summary>
        /// Gets the largest possible time instance
        /// </summary>
        public new static InstantTime MaxValue => Instant.MaxValue; 

        /// <summary>
        /// Gets the smallest possible time instance
        /// </summary>
        public new static InstantTime MinValue => Instant.MinValue; 
        
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
        public static bool operator <(InstantTime a, InstantTime b) => a.Instant < b.Instant;
        
        /// <summary>
        /// Less or equal than operator 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less than b </returns> 
        public static bool operator <=(InstantTime a, InstantTime b) => a.Instant <= b.Instant;

        /// <summary>
        /// Greater than operator 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a greter than b </returns> 
        public static bool operator >(InstantTime a, InstantTime b) => a.Instant > b.Instant;

        /// <summary>
        /// Greater or equal than operator 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less than b </returns> 
        public static bool operator >=(InstantTime a, InstantTime b) => a.Instant >= b.Instant;

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
        /// Gets the time instance from ticks string string
        /// </summary>
        /// <param name="ticks">Ticks string</param>
        /// <returns>Time instance</returns>
        public new static Time FromUnixTicks(string ticks)
        {
            return long.TryParse(ticks, out var time) ? new InstantTime(Instant.FromUnixTimeTicks(time)) : default(Time);
        }

        /// <summary>
        /// Creates an instance of <see cref="InstantTime"/> from the specified Unix time ticks.
        /// </summary>
        /// <param name="ticks">The number of ticks since the Unix epoch.</param>
        /// <returns>An <see cref="InstantTime"/> instance representing the specified Unix time ticks.</returns>
        public new static Time FromUnixTicks(long ticks) => new InstantTime(Instant.FromUnixTimeTicks(ticks));

        /// <inheritdoc />
        public override string ToExtendedIso() => InstantPattern.ExtendedIso.Format(Instant);

        /// <inheritdoc />
        public override string ToUnixTicks() => Instant.ToUnixTimeTicks().ToString();

        /// <inheritdoc />
        public override Instant ToInstant() => Instant;

        /// <inheritdoc />
        public override Time JustBefore() => new InstantTime(Instant - Duration.FromMilliseconds(1));

        /// <inheritdoc />
        protected override Duration DurationTo(Time other)
        {
            switch (other)
            {
                case null:
                    return Duration.Zero;
                case InstantTime instantTime:
                    return instantTime.Instant - Instant;
                default:
                    throw new InvalidCastException($"Object must be of type {this.GetType()}");
            }    
        }

        /// <inheritdoc />
        protected override Time AddDuration(Duration duration) => new InstantTime(Instant + duration);
        
        /// <inheritdoc />
        public override long ToUnixTimeMilliseconds() => Instant.ToUnixTimeMilliseconds();

        /// <summary>
        /// Creates an <see cref="InstantTime"/> instance from the specified Unix time in milliseconds.
        /// </summary>
        /// <param name="ms">The Unix time in milliseconds representing the number of milliseconds since the Unix epoch (January 1, 1970, 00:00:00 UTC).</param>
        /// <returns>An <see cref="InstantTime"/> instance corresponding to the specified Unix time in milliseconds.</returns>
        public new static InstantTime FromUnixTimeMilliseconds(long ms) => Instant.FromUnixTimeMilliseconds(ms);
        
        /// <inheritdoc />
        public override int CompareTo(Time other)
        {
            switch (other)
            {
                case null:
                    return 1;
                case InstantTime instantTime:
                    return Instant.CompareTo(instantTime.Instant);
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
            Instant.ToString(format, formatProvider);
    }
}