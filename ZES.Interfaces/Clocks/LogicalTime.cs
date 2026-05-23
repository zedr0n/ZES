using System;
using System.Diagnostics;
using NodaTime;
using NodaTime.Text;

#pragma warning disable SA1300
#pragma warning disable CS8907

namespace ZES.Interfaces.Clocks
{
    /// <summary>
    /// Logical time record
    /// </summary>
    [DebuggerDisplay("{ToExtendedIso(), nq}")]
    public sealed record LogicalTime(long l, long c) : Time, IComparable<LogicalTime>, IComparable
    {
        /// <summary>
        /// Gets the largest possible time instance
        /// </summary>
        public new static LogicalTime MaxValue { get; } = new(Instant.MaxValue.ToUnixTimeTicks(), 0);

        /// <summary>
        /// Gets the smallest possible time instance
        /// </summary>
        public new static LogicalTime MinValue { get; } = new(Instant.MinValue.ToUnixTimeTicks(), 0);

        /// <summary>
        /// Lexicographic comparison for logical time
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less than b by lexicographical comparison</returns>
        public static bool operator <(LogicalTime a, LogicalTime b)
        {
            if (a.l < b.l)
                return true;
            return a.c < b.c;
        }
        
        /// <summary>
        /// Lexicographic comparison for logical time
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less or equal than b by lexicographical comparison</returns>
        public static bool operator <=(LogicalTime a, LogicalTime b)
        {
            return a < b || a == b;
        }

        /// <summary>
        /// Lexicographic comparison for logical time
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a more than b by lexicographical comparison</returns>
        public static bool operator >(LogicalTime a, LogicalTime b)
        {
            return b < a;
        }

        /// <summary>
        /// Lexicographic comparison for logical time
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a more or equal than b by lexicographical comparison</returns>
        public static bool operator >=(LogicalTime a, LogicalTime b)
        {
            return a > b || a == b;
        }
        
        /// <summary>
        /// Gets the time instance from extended iso string
        /// </summary>
        /// <param name="time">Extended iso string</param>
        /// <returns>Time instance</returns>
        public new static LogicalTime FromExtendedIso(string time)
        {
            if (time == null)
                return default;
            var tokens = time.Split(';');
            long c = 0;
            if (tokens.Length > 1)
                c = long.Parse(tokens[1]);
            
            var parseResult = InstantPattern.ExtendedIso.Parse(tokens[0]);
            return parseResult.Success ? new LogicalTime(parseResult.Value.ToUnixTimeTicks(), c) : default;
        }

        /// <summary>
        /// Gets the time instance from ticks string string
        /// </summary>
        /// <param name="ticks">Ticks string</param>
        /// <returns>Time instance</returns>
        public new static Time FromUnixTicks(string ticks)
        {
            if (ticks == null)
                return default;
            
            var tokens = ticks.Split(';');
            long c = 0;
            if (tokens.Length > 1)
                c = long.Parse(tokens[1]);

            return long.TryParse(tokens[0], out var time) ? new LogicalTime(time, c) : default(Time);
        }

        /// <summary>
        /// Converts a Unix timestamp, expressed in ticks, to a LogicalTime instance.
        /// </summary>
        /// <param name="ticks">The Unix timestamp in ticks to be converted.</param>
        /// <returns>A LogicalTime instance representing the given Unix timestamp.</returns>
        public new static Time FromUnixTicks(long ticks) => new LogicalTime(ticks, 0);

        /// <inheritdoc />
        public int CompareTo(LogicalTime other)
        {
            if (this < other)
                return -1;
            if (this > other)
                return 1;
            return 0;
        }

        /// <inheritdoc />
        public override string ToExtendedIso()
        {
            var str = InstantPattern.ExtendedIso.Format(Instant.FromUnixTimeTicks(l));
            if (c > 0)
                str += $";{c}";
            return str;
        }
        
        /// <inheritdoc />
        public override string ToUnixTicks()
        {
            var str = l.ToString(); 
            if (c > 0)
                str += $";{c}";
            return str;
        }

        /// <inheritdoc />
        public override Instant ToInstant() => Instant.FromUnixTimeTicks(l);

        /// <inheritdoc />
        public override Time JustBefore() => new LogicalTime(l - 1, 0);

        /// <inheritdoc />
        public override long ToUnixTimeMilliseconds() => Instant.FromUnixTimeTicks(l).ToUnixTimeMilliseconds();

        /// <summary>
        /// Creates a <see cref="LogicalTime"/> instance from the specified Unix time in milliseconds.
        /// </summary>
        /// <param name="ms">The Unix time in milliseconds representing the number of milliseconds since the Unix epoch (January 1, 1970, 00:00:00 UTC).</param>
        /// <returns>A <see cref="LogicalTime"/> instance corresponding to the specified Unix time in milliseconds.</returns>
        public new static LogicalTime FromUnixTimeMilliseconds(long ms) => new LogicalTime(Instant.FromUnixTimeMilliseconds(ms).ToUnixTimeTicks(), 0);
        
        /// <inheritdoc />
        protected override Duration DurationTo(Time other)
        {
            switch (other)
            {
                case null:
                    return Duration.Zero;
                case LogicalTime logicalTime:
                    return Instant.FromUnixTimeTicks(logicalTime.l) - Instant.FromUnixTimeTicks(l);
                default:
                    throw new InvalidCastException($"Object must be of type {this.GetType()}");
            }    
        }

        /// <inheritdoc />
        protected override Time AddDuration(Duration duration) => new LogicalTime((Instant.FromUnixTimeTicks(l) + duration).ToUnixTimeTicks(), 0);

        /// <inheritdoc />
        public override int CompareTo(Time other)
        {
            switch (other)
            {
                case null:
                    return 1;
                case LogicalTime time:
                    return CompareTo(time);
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
                case LogicalTime time:
                    return CompareTo(time);
                default:
                    throw new InvalidCastException($"Object must be of type {this.GetType()}");
            }
        }

        /// <summary>
        /// Converts the logical time to its string representation using the extended ISO format.
        /// </summary>
        /// <returns>A string representation of the logical time in extended ISO format.</returns>
        public override string ToString() => ToExtendedIso();
        
        /// <inheritdoc />
        public override string ToString(string format, IFormatProvider formatProvider) =>
            Instant.FromUnixTimeTicks(l).ToString(format, formatProvider);
    }
}