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
        private const int ReadSize = 100;

        public static async Task<IStream> GetStream(
            this IStreamStore streamStore, string key)
        {
            var metadata = await streamStore.GetStreamMetadata(key);
            var parent = metadata.MetadataJson.ParseParent();
            var version = parent?.Version ?? -1;  
            
            var page = await streamStore.ReadStreamBackwards(key, StreamVersion.End, 1);
            if (page.Messages.Any())
                version += page.Messages.Single().StreamVersion + 1; 
            
            var theStream = new Stream(key, version) { Parent = parent };
            while (parent != null && parent.Version > ExpectedVersion.EmptyStream)
            {
                var parentMetadata = await streamStore.GetStreamMetadata(parent.Key);
                var grandParent = parentMetadata.MetadataJson.ParseParent();
                parent.Parent = grandParent;
                parent = grandParent;
            }

            return theStream;
        }
    }
}