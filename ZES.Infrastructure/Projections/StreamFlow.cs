using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    /// <inheritdoc />
    public class StreamFlow : Dataflow<IStream, int>
    {
        private readonly CancellationToken _token;
        private readonly ITargetBlock<IEvent> _eventBlock;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ILog _log;

        private readonly TransformBlock<IStream, int> _block;

        private int _version = ExpectedVersion.EmptyStream;

        private StreamFlow(
            DataflowOptions dataflowOptions,
            CancellationToken token,
            ITargetBlock<IEvent> eventBlock,
            IEventStore<IAggregate> eventStore,
            ILog log)
            : base(dataflowOptions)
        {
            _token = token;
            _eventBlock = eventBlock;
            _eventStore = eventStore;
            _log = log;
            token.Register(Complete);

            _block = new TransformBlock<IStream, int>(
                async s => await Read(s),
                dataflowOptions.ToExecutionBlockOption());

            RegisterChild(_block);
            
            InputBlock = _block;
            OutputBlock = _block;
        }

        /// <inheritdoc />
        public override ITargetBlock<IStream> InputBlock { get; }

        /// <inheritdoc />
        public override ISourceBlock<int> OutputBlock { get; }

        /// <inheritdoc />
        public override void Complete()
        {
            _block?.TryReceiveAll(out _);
            
            base.Complete();
        }
        
        private async Task<int> Read(IStream s)
        {
            _log?.Info($"{s.Key}@{s.Version} <- {_version}", $"{Parents.Select(p => p.Name).Aggregate((a, n) => a + n)}->{Name}");
            if (_version > s.Version)
                throw new InvalidOperationException($"Stream update is version {s.Version}, behind projection version {_version}");

            if (_version == s.Version || _token.IsCancellationRequested) 
                return _version;
            
            var o = _eventStore.ReadStream<IEvent>(s, _version + 1, s.Version - _version)
                .Publish().RefCount();

            o.Subscribe(async e => await _eventBlock.SendAsync(e), _token);

            await o.LastOrDefaultAsync().ToTask(_token);
            _version = s.Version;
            return _version;
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
            
            internal StreamFlow Bind(ITargetBlock<IEvent> eventBlock)
            {
                return new StreamFlow(_options, _token, eventBlock, _store, _log);        
            }
        }
    }
}