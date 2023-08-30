using System;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces
{
    /// <summary>
    /// Replica event id
    /// </summary>
    public record EventId(string ReplicaName, Time Timestamp) : IComparable<EventId>, IComparable
    {
        /// <summary>
        /// Operator less than for <see cref="EventId"/> 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less than b </returns>
        public static bool operator <(EventId a, EventId b) => a.Timestamp < b.Timestamp;

        /// <summary>
        /// Operator less or than for <see cref="EventId"/> 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a less or equal than b by lexicographical comparison</returns>
        public static bool operator <=(EventId a, EventId b) => a.Timestamp <= b.Timestamp;

        /// <summary>
        /// Operator greater than for <see cref="EventId"/> 
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a more than b </returns>
        public static bool operator >(EventId a, EventId b) => b.Timestamp > a.Timestamp;

        /// <summary>
        /// Lexicographic comparison for logical time
        /// </summary>
        /// <param name="a">Left value</param>
        /// <param name="b">Right value</param>
        /// <returns>True if a more or equal than b </returns>
        public static bool operator >=(EventId a, EventId b) => a.Timestamp >= b.Timestamp;

        /// <inheritdoc />
        public int CompareTo(EventId other) => Timestamp.CompareTo(other.Timestamp);

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
                throw new InvalidCastException($"EventId should be of format {nameof(Timestamp)}@{nameof(ReplicaName)}");
            return new EventId(ReplicaName: tokens[1], Timestamp: Time.Parse(tokens[0]));
        }
        
        /// <summary>
        /// Convert the associated timestamp to serialised string 
        /// </summary>
        /// <returns>Serialised string</returns>
        public string Serialise() => Timestamp.Serialise();
        
        /// <inheritdoc />
        public override string ToString() => $"{Serialise()}@{ReplicaName}";
    }
}