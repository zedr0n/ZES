using System;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces
{
    /// <summary>
    /// Replica event id
    /// </summary>
    public record EventId(string replicaName, Time timestamp) : IComparable<EventId>, IComparable
    {
        /// <summary>
        /// Operator less than for <see cref="EventId"/> 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less than b </returns>
        public static bool operator <(EventId a, EventId b) => a.timestamp < b.timestamp;

        /// <summary>
        /// Operator less or than for <see cref="EventId"/> 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less or equal than b by lexicographical comparison</returns>
        public static bool operator <=(EventId a, EventId b) => a.timestamp <= b.timestamp;

        /// <summary>
        /// Operator greater than for <see cref="EventId"/> 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a more than b </returns>
        public static bool operator >(EventId a, EventId b) => b.timestamp > a.timestamp;

        /// <summary>
        /// Lexicographic comparison for logical time
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a more or equal than b </returns>
        public static bool operator >=(EventId a, EventId b) => a.timestamp >= b.timestamp;

        /// <inheritdoc />
        public int CompareTo(EventId other) => timestamp.CompareTo(other.timestamp);

        /// <inheritdoc />
        public int CompareTo(object obj)
        {
            switch (obj)
            {
                case null:
                    return 1;
                case EventId eventId:
                    return CompareTo(eventId);
                default:
                    throw new InvalidCastException($"Object must be of type {this.GetType()}");
            }
        }

        /// <summary>
        /// Create the <see cref="EventId"/> from string
        /// </summary>
        /// <param name="str">String representation</param>
        /// <returns><see cref="EventId"/> instance</returns>
        public static EventId Parse(string str)
        {
            var tokens = str.Split('@');
            if (tokens.Length != 2)
                throw new InvalidCastException($"EventId should be of format {nameof(timestamp)}@{nameof(replicaName)}");
            return new EventId(replicaName: tokens[1], timestamp: Time.FromExtendedIso(tokens[0]));
        }
        
        /// <summary>
        /// Convert the associated timestamp to extended ISO
        /// </summary>
        /// <returns>Extended ISO string</returns>
        public string ToExtendedIso() => timestamp.ToExtendedIso();
        
        /// <inheritdoc />
        public override string ToString() => $"{ToExtendedIso()}@{replicaName}";
    }
}