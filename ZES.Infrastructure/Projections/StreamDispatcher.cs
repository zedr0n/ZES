using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    /// <inheritdoc />
    public partial class Projection<TState> : IProjection<TState>
    {
        private class StreamDispatcher : ParallelDataDispatcher<IStream, string>
        {
            private readonly IEventStore<IAggregate> _store;
            private readonly Action<IEvent> _when;
            private readonly CancellationTokenSource _source;
            private readonly ILog _log;
            private readonly ConcurrentDictionary<string, int> _versions;
            private int _parallelCount;

            public StreamDispatcher(
                ConcurrentDictionary<string, int> versions,
                DataflowOptions option,
                Action<IEvent> when,
                CancellationTokenSource source,
                IEventStore<IAggregate> store,
                ILog log)
                : base(s => s.Key, option)
            {
                _versions = versions;
                _when = when;
                _source = source;
                _store = store;
                _log = log;
            }

            protected override Dataflow<IStream> CreateChildFlow(string dispatchKey) =>
                new StreamFlow(_versions.GetOrAdd(dispatchKey, -1), _when, _source, _store);

            protected override async Task SendToChild(Dataflow<IStream> dataflow, IStream input)
            {
                Interlocked.Increment(ref _parallelCount);
                _log.Debug($"Projection parallel count : {_parallelCount}");

                await ((StreamFlow)dataflow).ProcessAsync(input);

                Interlocked.Decrement(ref _parallelCount);
            }

            protected override void CleanUp(Exception dataflowException)
            {
                if (dataflowException != null)
                    _log.Error($"Dataflow error : {dataflowException}");
                base.CleanUp(dataflowException);
            }

            private class StreamFlow : Dataflow<IStream>
            {
                private readonly BufferBlock<IStream> _inputBlock = new BufferBlock<IStream>();
                private int _version;
                private TaskCompletionSource<bool> _next = new TaskCompletionSource<bool>();

                public StreamFlow(
                    int version,
                    Action<IEvent> when,
                    CancellationTokenSource tokenSource,
                    IEventStore<IAggregate> eventStore)
                    : base(DataflowOptions.Default)
                {
                    _version = version;

                    var readBlock = new TransformBlock<IStream, IObservable<IEvent>>(
                        s => eventStore.ReadStream(s, _version + 1),
                        new ExecutionDataflowBlockOptions { CancellationToken = tokenSource.Token });

                    var updateBlock = new ActionBlock<IObservable<IEvent>>(
                        async o =>
                        {
                            o.Subscribe(
                                e =>
                                {
                                    when(e);
                                    _version = e.Version;
                                }, tokenSource.Token);
                            if (!await o.IsEmpty())
                                await o;

                            _next.SetResult(true);
                            _next = new TaskCompletionSource<bool>();
                        },
                        new ExecutionDataflowBlockOptions { CancellationToken = tokenSource.Token, MaxDegreeOfParallelism = 1 });

                    _inputBlock.LinkTo(
                        readBlock,
                        new DataflowLinkOptions { PropagateCompletion = true },
                        s => _version < s.Version);
                    readBlock.LinkTo(updateBlock);

                    RegisterChild(_inputBlock);
                    RegisterChild(readBlock);
                    RegisterChild(updateBlock);
                }

                public override ITargetBlock<IStream> InputBlock => _inputBlock;

                /// <summary>
                /// Processes a single event asynchronously by the dataflow 
                /// </summary>
                /// <param name="s">Updated stream</param>
                /// <returns>Task indicating whether the event was processed by the dataflow</returns>
                public async Task<bool> ProcessAsync(IStream s)
                {
                    if (!await _inputBlock.SendAsync(s))
                        return false;
                    return await _next.Task;
                }
            }
        }
    }
}