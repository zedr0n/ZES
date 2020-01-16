using System.Linq;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Serialization;
using ZES.Infrastructure.Streams;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Utils
{
    internal static class SqlStreamStoreExtensions
    {
        /// <summary>
        /// Enrich the stream from store
        /// </summary>
        /// <remarks>
        /// <para/>
        /// Null if stream exists but some of the ancestors do not
        /// </remarks>
        /// <param name="streamStore">Stream store</param>
        /// <param name="key">Stream key</param>
        /// <param name="serializer">Serializer</param>
        /// <returns>Stream info</returns>
        public static async Task<IStream> GetStream(
            this IStreamStore streamStore,  string key, ISerializer<IEvent> serializer)
        {
            var metadata = await streamStore.GetStreamMetadata(key);
            var stream = serializer.DecodeStreamMetadata(metadata.MetadataJson); 
            if (stream == null)
                return new Stream(key);

            var count = await streamStore.DeletedCount(key);
            stream.AddDeleted(count);

            var parent = stream.Parent;
            while (parent != null && parent.Version > ExpectedVersion.EmptyStream)
            {
                var parentMetadata = await streamStore.GetStreamMetadata(parent.Key);
                if (parentMetadata == null)
                    return null;

                count = await streamStore.DeletedCount(parent.Key);
                parent.AddDeleted(count);

                var grandParent = serializer.DecodeStreamMetadata(parentMetadata.MetadataJson)?.Parent; 
                
                parent.Parent = grandParent;
                parent = grandParent;
            }

            return stream;
        }
        
        public static async Task<int> LastPosition(this IStreamStore streamStore, string key)
        {
            var page = await streamStore.ReadStreamBackwards(key, StreamVersion.End, 1);
            if (page.Status == PageReadStatus.StreamNotFound)
                return ExpectedVersion.NoStream;
            
            if (page.Messages.Any())
                return page.Messages.Single().StreamVersion;
            
            return ExpectedVersion.EmptyStream;
        }

        private static async Task<int> DeletedCount(this IStreamStore streamStore, string key)
        {
            var deleted = 0;
            var page = await streamStore.ReadStreamForwards("$deleted", 0, Configuration.BatchSize);
            while (page.Messages.Length > 0)
            {
                deleted += page.Messages.Count(m => m.StreamId == key);
                page = await page.ReadNext();
            }

            return deleted;
        }
    }
}