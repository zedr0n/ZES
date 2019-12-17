using Newtonsoft.Json.Linq;
using ZES.Infrastructure.Streams;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Serialization
{
    internal static class JExtensions
    {
        public static string JStreamMetadata(IStream stream)
        {
            var meta = new JObject(
                new JProperty(nameof(IStream.Version), stream.Version));

            if (stream.Parent != null)
            {
                var parent = new JObject(
                    new JProperty(nameof(IStream.Key), stream.Parent?.Key),
                    new JProperty(nameof(IStream.Version), stream.Parent?.Version)); 
                meta.Add(nameof(IStream.Parent), parent);
            }

            return meta.ToString();
        }

        public static IStream ParseMetadata(this string json, string key)
        {
            if (json == null)
                return null;

            var jarray = JObject.Parse(json);
            
            if (!jarray.TryGetValue(nameof(IStream.Version), out var version))
                return null;

            var stream = new Stream(key, (int)version);

            if (!jarray.TryGetValue(nameof(IStream.Parent), out var jParent))
                return stream;
            
            ((JObject)jParent).TryGetValue(nameof(IStream.Key), out var parentKey);
            ((JObject)jParent).TryGetValue(nameof(IStream.Version), out var parentVersion);
                
            stream.Parent = new Stream((string)parentKey, (int)parentVersion);

            return stream;
        }
    }
}