using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Streams
{
    public class Stream : IStream
    {
        public Stream(string id, int version, string timeline = "")
        {
            _id = id;
            Version = version;
            Timeline = timeline;
        }
        
        public string Key => $"{Timeline}:{_id}";
        private readonly string _id;
        public int Version { get; set; }
        public string Timeline { get; set; }
    }
}