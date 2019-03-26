using System;
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

        public IStream Clone()
        {
            return Clone(Version);
        }
        
        public IStream Clone(int version)
        {
            if(!(MemberwiseClone() is Stream clone))
                throw new InvalidCastException();
            
            clone.Version = version;
            return clone;
        }

        public string Key => $"{TimelineId}:{_key}";
        private readonly string _key;
        public int Version { get; set; }
        public string TimelineId { get; set; } = "";
    }
}