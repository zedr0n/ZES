using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    public abstract partial class Projection<TState>
    {
        public partial class ProjectionDispatcher
        {
            public class StreamFlow : Dataflow<IStream, int>
            {
                private readonly ILog _log;
                private readonly IEventStore<IAggregate> _store;

                private readonly Lazy<Task> _start;
                private readonly CancellationTokenSource _cancellation; 
                
                private readonly ConcurrentDictionary<string, int> _version = new ConcurrentDictionary<string, int>();
                private readonly ConcurrentDictionary<int, CancellationTokenSource> _cancels = new ConcurrentDictionary<int, CancellationTokenSource>();

                private long _timestamp;

                private Action<IEvent> _when = e => { };

                private TransformBlock<IStream, int> _readBlock = new TransformBlock<IStream, int>(s =>
                {
                    throw new InvalidOperationException();
                    return null;
                });

                private BufferBlock<IEvent> _eventBlock;
                private ActionBlock<IEvent> _whenBlock;

                private StreamFlow(
                    DataflowOptions options,
                    CancellationTokenSource cancelSource,
                    Lazy<Task> delay,
                    IEventStore<IAggregate> store,
                    ILog log)
                    : base(options)
                {
                    _store = store;
                    _log = log;

                    _start = delay;
                    _cancellation = cancelSource;
                }

                public override ITargetBlock<IStream> InputBlock => _readBlock;
                public override ISourceBlock<int> OutputBlock => _readBlock;

                public override void Complete()
                {
                    _eventBlock.Complete();
                    _whenBlock.Complete();
                    base.Complete();
                }

                /// <inheritdoc />
                protected override void CleanUp(Exception dataflowException)
                {
                    foreach (var key in _cancels.Keys)
                    {
                        _cancels.TryRemove(key, out var c);
                        c?.Cancel();
                    }
                }
                
                private StreamFlow Bind(Action<IEvent> when)
                {
                    _when = when;
                            
                    _eventBlock = new BufferBlock<IEvent>();
                    _whenBlock = new ActionBlock<IEvent>(e =>
                    {
                        if ( e.Timestamp < _timestamp )
                            throw new InvalidOperationException();
                                 
                        _when(e);
                        _timestamp = e.Timestamp;
                    });

                    _readBlock = new TransformBlock<IStream, int>(
                        async s => await Transform(s), DataflowOptions.ToExecutionBlockOption());
                        
                    RegisterChild(_whenBlock);
                    RegisterChild(_readBlock);
                    RegisterChild(_eventBlock);

                    // Start updating the projection only after streams were all read ( if during rebuild )
                    _start.Value.ContinueWith(async t =>
                    {
                        if (!t.IsFaulted && !t.IsCanceled)
                        {
                            if (_eventBlock.TryReceiveAll(out var events))
                                await _whenBlock.ToDataflow().ProcessAsync(events.OrderBy(e => e.Timestamp), false);

                            // _log.Trace("Listening to streams", this);
                            _eventBlock.LinkTo(_whenBlock, e => _start.Value.IsCompleted);
                        }
                    });

                    return this;
                }

                private async Task Rebuild()
                {
                    
                }

                private async Task<int> Transform(IStream s)
                {
                    _log?.Trace($"{s.Key}@{s.Version}", this);
                    var version = _version.GetOrAdd(s.Key, ExpectedVersion.EmptyStream);
                    
                    // need to copy here as IStream object might get updated in the meantime 
                    // resulting in version set  to wrong value in the dictionary
                    var streamVersion = s.Version;
                    if (version > streamVersion)
                        throw new InvalidOperationException($"Stream update is version {s.Version}, behind projection version {version}");

                    if (version == streamVersion)
                        return version;

                    // read the new events from the stream with a token linked to the rebuild token
                    // disposing of token on completion
                    var source = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
                    _cancels.GetOrAdd(s.GetHashCode(), source);

                    // _log?.Trace($"Reading {streamVersion - version} events from {s.Key}", this);
                    var o = _store.ReadStream<IEvent>(s, version + 1, streamVersion - version)
                        .Publish().RefCount();

                    o.Finally(() => _cancels.TryRemove(s.GetHashCode(), out _))
                        .Subscribe(async e => await _eventBlock.SendAsync(e, source.Token), source.Token);

                    if (!_version.TryUpdate(s.Key, streamVersion, version))
                        throw new InvalidOperationException();

                    await o.LastOrDefaultAsync();
                    return _version[s.Key]; 
                }

                public class Builder : FluentBuilder
                {
                    private readonly ILog _log;
                    private readonly IEventStore<IAggregate> _store;

                    private DataflowOptions _options = DataflowOptions.Default;
                    private CancellationTokenSource _cancellation = new CancellationTokenSource();
                    private Lazy<Task> _delay = new Lazy<Task>(() => Task.CompletedTask);
                    
                    public Builder(ILog log, IEventStore<IAggregate> store)
                    {
                        _log = log;
                        _store = store;
                    }

                    internal Builder WithOptions(DataflowOptions options)
                        => Clone(this, b => b._options = options);

                    internal Builder WithCancellation(CancellationTokenSource source)
                        => Clone(this, b => b._cancellation = source);

                    internal Builder DelayUntil(Lazy<Task> delay)
                        => Clone(this, b => b._delay = delay);

                    internal Dataflow<IStream, int> Bind(Action<IEvent> when)
                    {
                        var flow = new StreamFlow(_options, _cancellation, _delay, _store, _log);
                        return flow.Bind(when);
                    }
                }
            }
        }
    }
}