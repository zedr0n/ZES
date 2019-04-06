using System;
using SqlStreamStore.Streams;
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

        public Stream(string key, int version = ExpectedVersion.NoStream)
        {
            var tokens = key.Split(':');
            if (tokens.Length != 2)
                throw new InvalidOperationException();
            
            Timeline = tokens[0];
            _id = tokens[1];
            Version = version;
        }

        public static Stream Branch(string key, string timeline)
        {
            return new Stream(key) { Timeline = timeline};
        }
        
        public string Key => $"{Timeline}:{_id}";
        private readonly string _id;
        public int Version { get; set; }
        public string Timeline { get; set; }
    }
}