using System;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using SqlStreamStore.Streams;
using ZES.Infrastructure.EventStore;
using ZES.Interfaces;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Utils
{
    /// <summary>
    /// Encoding extensions for <see cref="SqlStreamStore"/> serialization
    /// </summary>
    public static class EventExtensions
    {
        /// <summary>
        /// Decode the stream message to IEventMetadata
        /// </summary>
        /// <param name="serializer">Event serializer</param>
        /// <param name="streamMessage">Stream message</param>
        /// <typeparam name="T">Type of stream message</typeparam>
        /// <returns>Deserialized metadata</returns>
        public static IEventMetadata DecodeMetadata<T>(this ISerializer<IEvent> serializer, T streamMessage)
        {
            var json = string.Empty;
            if (streamMessage is StreamMessage m)
                json = m.JsonMetadata;
            if (streamMessage is RecordedEvent recordedEvent)
                json = Encoding.UTF8.GetString(recordedEvent.Metadata);
            return serializer.DecodeMetadata(json);
        }

        /// <summary>
        /// Decode the stream message to IEvent
        /// </summary>
        /// <param name="serializer">Event serializer</param>
        /// <param name="streamMessage">Stream message</param>
        /// <typeparam name="T">Type of stream message</typeparam>
        /// <returns>Deserialized event</returns>
        public static async Task<IEvent> DecodeEvent<T>(this ISerializer<IEvent> serializer, T streamMessage)
        {
            var json = string.Empty;
            if (streamMessage is StreamMessage m)
                json = await m.GetJsonData();
            if (streamMessage is RecordedEvent recordedEvent)
                json = Encoding.UTF8.GetString(recordedEvent.Data);
            return serializer.Deserialize(json);
        }

        /// <summary>
        /// Decode the stream message to IEvent or IEventMetadata
        /// </summary>
        /// <param name="serializer">Event serializer</param>
        /// <param name="streamMessage">Stream message</param>
        /// <typeparam name="TMessage">Type of stream message</typeparam>
        /// <typeparam name="T">Event or metadatq</typeparam>
        /// <returns>Deserialized event</returns>
        public static async Task<T> Decode<TMessage, T>(this ISerializer<IEvent> serializer, TMessage streamMessage)
            where T : class, IEventMetadata
        {
            if (typeof(T) == typeof(IEvent))
                return await serializer.DecodeEvent(streamMessage) as T;
            if (typeof(T) == typeof(IEventMetadata))
                return serializer.DecodeMetadata(streamMessage) as T;
            return null;
        }

        /// <summary>
        /// Extend the serializer to encode events 
        /// </summary>
        /// <param name="serializer">Serializer instance</param>
        /// <param name="e">Event to encode</param>
        /// <typeparam name="T">Encoded message type</typeparam>
        /// <returns>Encoded message</returns>
        public static T Encode<T>(this ISerializer<IEvent> serializer, IEvent e)
        {
            if (typeof(T) == typeof(NewStreamMessage))
                return (T)(object)EncodeSql(serializer, e);
            if (typeof(T) == typeof(EventData))
                return (T)(object)EncodeTcp(serializer, e);

            throw new NotImplementedException($"Encoding for {typeof(T).Name} not implemented");
        }

        /// <summary>
        /// Extend the serializer to encode for <see cref="EventStore"/>
        /// </summary>
        /// <param name="serializer">Serializer instance</param>
        /// <param name="e">Event to encode</param>
        /// <returns>Encoded message</returns>
        public static EventData EncodeTcp(this ISerializer<IEvent> serializer, IEvent e)
        {
            serializer.SerializeEventAndMetadata(e, out var eventJson, out var metadataJson);

            return new EventData(
                e.MessageId,
                e.MessageType,
                true,
                Encoding.UTF8.GetBytes(eventJson),
                Encoding.UTF8.GetBytes(metadataJson));
        }
        
        /// <summary>
        /// Extend the serializer to encode for <see cref="SqlStreamStore"/>
        /// </summary>
        /// <param name="serializer">Serializer instance</param>
        /// <param name="e">Event to encode</param>
        /// <returns>Encoded message</returns>
        public static NewStreamMessage EncodeSql(this ISerializer<IEvent> serializer, IEvent e)
        {
            serializer.SerializeEventAndMetadata(e, out var eventJson, out var metadataJson);
            
            // return new NewStreamMessage(e.MessageId, e.MessageType, serializer.Serialize(e), serializer.EncodeMetadata(e));
            return new NewStreamMessage(e.MessageId, e.MessageType, eventJson, metadataJson);
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