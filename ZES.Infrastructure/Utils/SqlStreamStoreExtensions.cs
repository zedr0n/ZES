using System;
using System.Linq;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Serialization;
using ZES.Infrastructure.Streams;
using ZES.Interfaces.EventStore;

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
        /// <returns>Stream info</returns>
        public static async Task<IStream> GetStream(
            this IStreamStore streamStore, string key)
        {
            var metadata = await streamStore.GetStreamMetadata(key);
            
            var parent = metadata.MetadataJson.ParseParent();
            var version = parent?.Version ?? -1;  
            
            var page = await streamStore.ReadStreamBackwards(key, StreamVersion.End, 1);
            if (page.Status == PageReadStatus.StreamNotFound)
                return new Stream(key);
            
            if (page.Messages.Any())
                version += page.Messages.Single().StreamVersion + 1; 
            
            var theStream = new Stream(key, version) { Parent = parent };
            while (parent != null && parent.Version > ExpectedVersion.EmptyStream)
            {
                var parentMetadata = await streamStore.GetStreamMetadata(parent.Key);
                if (parentMetadata == null)
                    return null;
                
                var grandParent = parentMetadata?.MetadataJson.ParseParent();
                parent.Parent = grandParent;
                parent = grandParent;
            }

            return theStream;
        }

        public static async Task<int> LastPosition(this IStreamStore streamStore, string key)
        {
            var page = await streamStore.ReadStreamBackwards(key, StreamVersion.End, 1);
            
            if (page.Messages.Any())
                return page.Messages.Single().StreamVersion;
            
            return ExpectedVersion.EmptyStream;
        }
    }
}