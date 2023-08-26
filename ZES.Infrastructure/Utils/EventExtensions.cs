using System;
using Newtonsoft.Json;
using ZES.Infrastructure.EventStore;
using ZES.Interfaces;

namespace ZES.Infrastructure.Utils
{
    /// <summary>
    /// Event extensions 
    /// </summary>
    public static class EventExtensions
    {
        /// <inheritdoc />
        public class MessageIdConverter : JsonConverter
        {
            /// <inheritdoc />
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var messageId = value as MessageId;
                writer.WriteValue(messageId.ToString()); 
            }

            /// <inheritdoc />
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var str = reader.Value?.ToString();
                if (str == null)
                    return null;
                return (MessageId)MessageId.Parse(str);
            }

            /// <inheritdoc />
            public override bool CanConvert(Type objectType) => objectType == typeof(MessageId);
        }
        
        /// <inheritdoc />
        public class EventIdConverter : JsonConverter
        {
            /// <inheritdoc />
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var eventId = value as EventId;
                writer.WriteValue(eventId.ToString()); 
            }

            /// <inheritdoc />
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var str = reader.Value.ToString();
                if (str == null)
                    return null;
                return (EventId)EventId.Parse(str);
            }

            /// <inheritdoc />
            public override bool CanConvert(Type objectType) => objectType == typeof(EventId);
        }
        
        /// <summary>
        /// Get the aggregate root id for the event
        /// </summary>
        /// <param name="e">Event instance</param>
        /// <returns>Aggregate root id</returns>
        public static string AggregateRootId(this IEvent e)
        {
            var stream = e.OriginatingStream ?? e.Stream;
            return new Stream(stream).Id;
        }
    }
}