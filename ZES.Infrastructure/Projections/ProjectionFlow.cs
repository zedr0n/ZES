using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    internal class ProjectionFlow<TState> : Dataflow<Tracked<IStream>>
        where TState : new()
    {
        private readonly ProjectionBase<TState> _projection;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ILog _log;
        private readonly CancellationToken _token;
            
        private readonly ConcurrentDictionary<string, int> _versions;
            
        public ProjectionFlow(DataflowOptions dataflowOptions, ProjectionBase<TState> projection)
            : base(dataflowOptions)
        {
            _projection = projection;
            _eventStore = projection.EventStore;
            _log = projection.Log;
            _token = projection.CancellationToken;
            _versions = projection.Versions;
                
            var block = new ActionBlock<Tracked<IStream>>(Process);
            RegisterChild(block);
            InputBlock = block;
        }

        /// <inheritdoc />
        public override ITargetBlock<Tracked<IStream>> InputBlock { get; }

        private async Task Process(Tracked<IStream> trackedStream)
        {
            var s = trackedStream.Value;
                
            if (s.Version == ExpectedVersion.NoStream)
            {
                trackedStream.Complete();
                return;
            }

            _token.Register(trackedStream.Complete);

            var version = _versions.GetOrAdd(s.Key, ExpectedVersion.EmptyStream);

            if (s.Version <= ExpectedVersion.EmptyStream)
                s.Version = 0;

            if (version > s.Version) 
                _log?.Warn($"Stream {s.Key} update is version {s.Version}, behind projection version {version}", GetDetailedName());

            if (version < s.Version)
            {
                _log?.Debug($"{s.Key}@{s.Version} <- {version}", $"{Parents.Select(p => p.Name).Aggregate((a, n) => a + n)}->{Name}");
                    
                var origVersion = version;
                await _eventStore.ReadStream<IEvent>(s, version + 1)
                    .TakeWhile(_ => !_token.IsCancellationRequested)
                    .Select(e =>
                    {
                        _projection.When(e);
                        version++;
                        return true;
                    })
                    .Timeout(Configuration.Timeout)
                    .LastOrDefaultAsync();
                    
                if (!_versions.TryUpdate(s.Key, version, origVersion))
                    throw new InvalidOperationException("Failed updating concurrent versions of projections");
            }
                
            trackedStream.Complete();
        }
    }
}