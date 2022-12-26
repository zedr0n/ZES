using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using ZES.Infrastructure;
using ZES.Infrastructure.EventStore;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Serialization;
using ExpectedVersion = ZES.Infrastructure.EventStore.ExpectedVersion;

namespace ZES.Persistence.EventStoreDB
{
    /// <summary>
    /// EventStore extensions
    /// </summary>
    public static class TcpEventStoreExtensions
    {
        /// <summary>
        /// Enrich the stream from store
        /// </summary>
        /// <remarks>
        /// <para/>
        /// Null if stream exists but some of the ancestors do not
        /// </remarks>
        /// <param name="connection">Stream store</param>
        /// <param name="key">Stream key</param>
        /// <param name="serializer">Serializer</param>
        /// <returns>Stream info</returns>
        public static async Task<IStream> GetStream(
            this IEventStoreConnection connection,  string key, ISerializer<IEvent> serializer)
        {
            var metadata = await connection.GetStreamMetadataAsync(key);
            var stream = serializer.DecodeStreamMetadata(metadata.StreamMetadata.AsJsonString()); 
            if (stream == null)
                return new Stream(key);

            var count = await connection.DeletedCount(key);
            stream.AddDeleted(count);

            var parent = stream.Parent;
            while (parent != null && parent.Version > ExpectedVersion.EmptyStream)
            {
                var parentMetadata = await connection.GetStreamMetadataAsync(parent.Key);
                if (parentMetadata.StreamMetadata == null)
                    return null;

                count = await connection.DeletedCount(parent.Key);
                parent.AddDeleted(count);

                var grandParent = serializer.DecodeStreamMetadata(parentMetadata.StreamMetadata.AsJsonString())?.Parent; 
                
                parent.Parent = grandParent;
                parent = grandParent;
            }

            return stream;
        }

        /// <summary>
        /// Gets the deleted count for the stream
        /// </summary>
        /// <param name="connection">Event Store connection</param>
        /// <param name="key">Stream key</param>
        /// <returns>Number of deleted events</returns>
        public static async Task<int> DeletedCount(this IEventStoreConnection connection, string key)
        {
            var deleted = 0;
            StreamEventsSlice slice;
            do
            {
                slice = await connection.ReadStreamEventsForwardAsync("$deleted", 0, Configuration.BatchSize, false);
                foreach (var r in slice.Events)
                {
                    var json = Encoding.UTF8.GetString(r.Event.Data);
                    if (json.Contains(key))
                        deleted++;
                }
            } 
            while (!slice.IsEndOfStream);

            return deleted;
        }
    }
}