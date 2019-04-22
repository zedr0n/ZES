using System;
using SqlStreamStore.Streams;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Streams
{
    public class Stream : IStream
    {
        private readonly string _id;
        private readonly string _type;
        
        public Stream(string id, string type, int version, string timeline = "")
        {
            _id = id;
            _type = type;
            Version = version;
            Timeline = timeline;
        }

        public Stream(string key, int version = ExpectedVersion.NoStream)
        {
            var tokens = key.Split(':');
            if (tokens.Length != 3)
                throw new InvalidOperationException();
            
            Timeline = tokens[0];
            _type = tokens[1];
            _id = tokens[2];
            Version = version;
        }

        public string Key => $"{Timeline}:{_type}:{_id}";

        public int Version { get; set; }
        public int UpdatedOn { get; set; }
        public string Timeline { get; set; }
        
        public static Stream Branch(string key, string timeline)
        {
            return new Stream(key) { Timeline = timeline };
        }
    }
}