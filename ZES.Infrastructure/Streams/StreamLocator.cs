using System;
using System.Collections.Concurrent;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Streams
{
    public class StreamLocator<I> : IStreamLocator<I> where I : IEventSourced
    {
        private readonly ConcurrentDictionary<string, IStream> _streams = new ConcurrentDictionary<string, IStream>();

        public StreamLocator(IEventStore<I> eventStore)
        {
            eventStore.Streams.Subscribe(stream => GetOrAdd(stream));
        }

        public IStream Find<T>(string id, string timeline = "master") where T : I
        {
            var aStream = new Stream(id, ExpectedVersion.NoStream, timeline);
            return _streams.TryGetValue(aStream.Key, out var stream) ? stream : default(IStream);
        }

        public IStream GetOrAdd(I es, string timeline = "master")
        {
            if (es == null)
                return default(IStream);

            var stream = new Stream(es.Id,ExpectedVersion.NoStream,timeline);
            return _streams.GetOrAdd(stream.Key, stream);
        }

        public IStream GetOrAdd(IStream stream)
        {
            return _streams.GetOrAdd(stream.Key, stream);
        }
    }
}