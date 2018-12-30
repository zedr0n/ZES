using System;
using System.Collections.Concurrent;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Streams
{
    public class StreamLocator : IStreamLocator
    {
        private readonly ConcurrentDictionary<string, IStream> _streams = new ConcurrentDictionary<string, IStream>();

        public StreamLocator(IEventStore eventStore)
        {
            eventStore.Streams.Subscribe(stream => GetOrAdd(stream));
        }

        public string Key(IAggregate es)
        {
            return "aggregate-"+es.Id;
        }
        
        public string Key(ISaga es)
        {
            return "saga-"+es.Id;
        }

        public IStream Find(string key)
        {
            _streams.TryGetValue(key, out var stream);
            return stream;
        }

        public IStream GetOrAdd(IEventSourced es)
        {
            var key = "NA";
            switch (es)
            {
                case IAggregate aggregate:
                    key = Key(aggregate);
                    break;
                case ISaga saga:
                    key = Key(saga);
                    break;
            }
            
            var stream = new Stream(key,es.Version);
            return _streams.GetOrAdd(key, stream);
        }

        public IStream GetOrAdd(IStream stream)
        {
            return _streams.GetOrAdd(stream.Key, stream);
        }
    }
}