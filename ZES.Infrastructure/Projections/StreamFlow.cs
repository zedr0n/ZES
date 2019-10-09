using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    /// <inheritdoc />
    public class StreamFlow : Dataflow<IStream, int>
    {
        private readonly ConcurrentDictionary<string, int> _versions;
        private readonly CancellationToken _token;
        private readonly ITargetBlock<IEvent> _eventBlock;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ILog _log;

        private readonly TransformBlock<IStream, int> _block;

        private StreamFlow(
            ConcurrentDictionary<string, int> versions,
            DataflowOptions dataflowOptions,
            CancellationToken token,
            ITargetBlock<IEvent> eventBlock,
            IEventStore<IAggregate> eventStore,
            ILog log)
            : base(dataflowOptions)
        {
            _versions = versions;
            _token = token;
            _eventBlock = eventBlock;
            _eventStore = eventStore;
            _log = log;

            _block = new TransformBlock<IStream, int>(
                async s => await Read(s),
                dataflowOptions.ToExecutionBlockOption());
            
            RegisterChild(_block);
        }

        /// <inheritdoc />
        public override ITargetBlock<IStream> InputBlock => _block;

        /// <inheritdoc />
        public override ISourceBlock<int> OutputBlock => _block;

        /// <inheritdoc />
        public override void Complete()
        {
            _block?.TryReceiveAll(out _);
            
            base.Complete();
        }
        
        private async Task<int> Read(IStream s)
        {
            var version = _versions.GetOrAdd(s.Key, ExpectedVersion.EmptyStream);
            _log?.Debug($"{s.Key}@{s.Version} <- {version}", $"{Parents.Select(p => p.Name).Aggregate((a, n) => a + n)}->{Name}");

            if (s.Version <= ExpectedVersion.EmptyStream)
                s.Version = 0;

            if (version > s.Version)
                _log?.Warn($"Stream update is version {s.Version}, behind projection version {version}", GetDetailedName());
            
            if (version >= s.Version || _token.IsCancellationRequested) 
                return version;
            
            var origVersion = version;
            await _eventStore.ReadStream<IEvent>(s, version + 1)
                .TakeWhile(_ => !_token.IsCancellationRequested)
                .Do(async e =>
                {
                    version++;
                    await _eventBlock.SendAsync(e, _token);
                })
                .LastOrDefaultAsync();

            if (!_versions.TryUpdate(s.Key, s.Version, origVersion))
                throw new InvalidOperationException("Failed updating concurrent versions of projections");

            return s.Version;
        }

        /// <inheritdoc />
        public class Builder : FluentBuilder
        {
            private readonly IEventStore<IAggregate> _store;
            private readonly ILog _log;
            private CancellationToken _token = CancellationToken.None;
            private DataflowOptions _options = DataflowOptions.Default;

            /// <summary>
            /// Initializes a new instance of the <see cref="Builder"/> class.
            /// </summary>
            /// <param name="store">Event store</param>
            /// <param name="log">Log helper</param>
            public Builder(IEventStore<IAggregate> store, ILog log)
            {
                _store = store;
                _log = log;
            }

            internal Builder WithOptions(DataflowOptions options)
                => Clone(this, b => b._options = options);

            internal Builder WithCancellation(CancellationToken token)
                => Clone(this, b => b._token = token);
            
            internal StreamFlow Bind(ITargetBlock<IEvent> eventBlock, ConcurrentDictionary<string, int> versions)
            {
                return new StreamFlow(versions, _options, _token, eventBlock, _store, _log);        
            }
        }
    }
}