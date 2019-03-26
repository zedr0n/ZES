using System;
using System.Collections.Concurrent;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Streams
{
    public class StreamLocator<I> : IStreamLocator<I> where I : IEventSourced
    {
        private readonly ConcurrentDictionary<string, IStream> _streams = new ConcurrentDictionary<string, IStream>();

        public StreamLocator(IEventStore<I> eventStore)
        {
            eventStore.Streams.Subscribe(stream => GetOrAdd(stream));
        }

        public IStream Find<T>(string id, string timeline = "") where T : I
        {
            var key = $"{timeline}:{id}";
            _streams.TryGetValue(key, out var stream);            
            return stream;
        }

        public IStream GetOrAdd(I es, string timeline = "")
        {
            var key = $"{timeline}:{es.Id}";
            var stream = new Stream(key,ExpectedVersion.NoStream);
            return _streams.GetOrAdd(key, stream);
        }

        public IStream GetOrAdd(IStream stream)
        {
            return _streams.GetOrAdd(stream.Key, stream);
        }
    }
}