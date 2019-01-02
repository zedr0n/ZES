using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Streams
{
    public class Stream : IStream
    {
        public Stream(string key, int version)
        {
            _key = key;
            Version = version;
        }

        public bool IsSaga => _key.Contains(":saga");
        public string Key => $"{TimelineId}:{_key}";
        private readonly string _key;
        public int Version { get; set; }
        public string TimelineId { get; set; } = "";
    }
}