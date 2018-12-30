using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Streams
{
    public class Stream : IStream
    {
        public Stream(string key, int version)
        {
            Key = key;
            Version = version;
        }
        public string Key { get; }
        public int Version { get; set; }
        public string TimelineId { get; set; }
    }
}