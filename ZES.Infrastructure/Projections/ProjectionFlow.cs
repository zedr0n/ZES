using System;
using System.ComponentModel;
using System.Linq;
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
    public abstract partial class Projection<TState> : IProjection<TState>
    {
        /// <inheritdoc />
        public class ProjectionFlow : ParallelDataDispatcher<string, IStream, int>
        {
            private readonly ILog _log;

            private readonly BufferBlock<IEvent> _eventBlock;
            
            private readonly Task _start;
            private readonly StreamFlow.Builder _builder;
            private readonly CancellationToken _cancellation;

            private long _timestamp;

            private Action<IEvent> _when = e => { };

            private ActionBlock<IEvent> _whenBlock;

            private ProjectionFlow(
                DataflowOptions options,
                CancellationToken token,
                Task delay,
                StreamFlow.Builder builder,
                ILog log)
                : base(s => s.Key, options, token, typeof(ProjectionFlow))
            {
                _log = log;

                _start = delay;
                _builder = builder;
                _cancellation = token;
                
                _eventBlock = new BufferBlock<IEvent>();
                RegisterChild(_eventBlock);

                token.Register(Complete);
            }

            /// <inheritdoc />
            public override void Complete()
            {
                ProcessEvents().Wait();
                _eventBlock.Complete();
                _whenBlock?.Complete();
               
                base.Complete();
            }
            
            /// <inheritdoc />
            protected override Dataflow<IStream, int> CreateChildFlow(string dispatchKey)
            {
                return _builder.WithOptions(DataflowOptions)
                    .WithCancellation(_cancellation)
                    .Bind(_eventBlock);
            }

            private ProjectionFlow Bind(Action<IEvent> when)
            {
                _when = when;
                _whenBlock = new ActionBlock<IEvent>(e =>
                {
                    // _log?.Debug($"{e.Stream}:{e.Version}", this);
                    if ( e.Timestamp < _timestamp )
                       throw new InvalidOperationException();

                    if (_cancellation.IsCancellationRequested) 
                        return;
                    
                    _when(e);
                    _timestamp = e.Timestamp;
                });

                RegisterChild(_whenBlock);

                // Start updating the projection only after precedence task ( e.g. rebuild ) was completed
                // guarantees that new events don't get processed before it was at least rebuilt
                if (_start == null)
                    return this;

                _start.ContinueWith(
                    t =>
                    {
                        if (t.IsSuccessful()) 
                            _eventBlock.LinkTo(_whenBlock, new DataflowLinkOptions { PropagateCompletion = true }); 
                    }, _cancellation);
                
                return this;
            }

            private async Task ProcessEvents()
            {
                // _log?.Debug($"Processing {_eventBlock.Count} events", this);
                if ( _eventBlock.TryReceiveAll(out var events) && !_cancellation.IsCancellationRequested ) 
                {
                    var count = events.Count;
                    
                    foreach (var e in events.OrderBy(x => x.Timestamp))
                        await _whenBlock.SendAsync(e, _cancellation);
                    
                    // var keys = events.Select(e => e.Stream).Distinct().Aggregate((a, v) => a + $", {v}");
                    _log?.Debug( $"Processed {count} events during rebuild", this);
                }
            }

            /// <inheritdoc />
            public class Builder : FluentBuilder
            {
                private readonly ILog _log;
                private readonly StreamFlow.Builder _builder;

                private DataflowOptions _options = DataflowOptions.Default;
                private CancellationToken _cancellation = CancellationToken.None; 
                private Task _delay;

                /// <summary>
                /// Initializes a new instance of the <see cref="Builder"/> class.
                /// </summary>
                /// <param name="log">Log helper</param>
                /// <param name="builder">Stream flow builder</param>
                public Builder(ILog log, StreamFlow.Builder builder)
                {
                    _log = log;
                    _builder = builder;
                }

                internal Builder WithOptions(DataflowOptions options)
                    => Clone(this, b => b._options = options);

                internal Builder WithCancellation(CancellationToken source)
                    => Clone(this, b => b._cancellation = source);

                internal Builder DelayUntil(Task delay)
                    => Clone(this, b => b._delay = delay);

                internal Dataflow<IStream, int> Bind(Action<IEvent> when)
                {
                    var flow = new ProjectionFlow(_options, _cancellation, _delay, _builder, _log);
                    return flow.Bind(when);
                }
            }
        }
    }
}