using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ZES.Infrastructure.Streams;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Serialization
{
    internal static class JExtensions
    {
        public static JProperty JTimestamp(this IMessage m)
        {
            return new JProperty(nameof(IMessage.Timestamp), m.Timestamp);        
        }

        public static JProperty JVersion(this IMessage m)
        {
            var version = (m as IEvent)?.Version ?? 0;
            return new JProperty(nameof(IEvent.Version), version);
        }

        public static string JParent(string key, int version )
        {
            return new JObject(new JProperty(nameof(IStream.Key), key), new JProperty(nameof(IStream.Version), version))
                .ToString();
        }

        public static IStream ParseParent(this string json)
        {
            if (json == null)
                return null;
            var jarray = JObject.Parse(json);

            if (!jarray.TryGetValue(nameof(IStream.Key), out var key))
                return null;

            if (!jarray.TryGetValue(nameof(IStream.Version), out var version))
                return null;

            var stream = new Stream((string)key, (int)version);
            return stream;
        }
        
        public static long? ParseTimestamp(this string json)
        {
            var jarray = JObject.Parse(json);
            jarray.TryGetValue(nameof(IMessage.Timestamp), out var timestamp);

            return (long?)timestamp;
        }

        public static int? ParseVersion(this string json)
        {
            var jarray = JObject.Parse(json);
            jarray.TryGetValue(nameof(IEvent.Version), out var version);

            return (int?)version;
        }
    }
}