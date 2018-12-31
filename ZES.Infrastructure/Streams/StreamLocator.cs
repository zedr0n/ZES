using System;
using System.Collections.Concurrent;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Streams
{
    public class StreamLocator : IStreamLocator
    {
        private readonly ConcurrentDictionary<string, IStream> _streams = new ConcurrentDictionary<string, IStream>();

        public StreamLocator(IEventStore eventStore)
        {
            eventStore.Streams.Subscribe(stream => GetOrAdd(stream));
        }

        public IStream Find<T>(string id, string timeline = "") where T:IEventSourced
        {
            var key = "NA";
            if(typeof(IAggregate).IsAssignableFrom(typeof(T)))
                key = $"{timeline}:root:{id}";
            else if(typeof(ISaga).IsAssignableFrom(typeof(T)))
                key = $"{timeline}:saga:{id}";
            
            _streams.TryGetValue(key, out var stream);            
            return stream;
        }

        public IStream GetOrAdd(IEventSourced es, string timeline = "")
        {
            var key = "NA";
            switch (es)
            {
                case IAggregate _:
                    key = $"{timeline}:root:{es.Id}";
                    break;
                case ISaga _:
                    key = $"{timeline}:saga:{es.Id}";
                    break;
            }

            if(key == "NA")
                throw new InvalidOperationException();

            var stream = new Stream(key,ExpectedVersion.NoStream);
            return _streams.GetOrAdd(key, stream);
        }

        public IStream GetOrAdd(IStream stream)
        {
            return _streams.GetOrAdd(stream.Key, stream);
        }
    }
}