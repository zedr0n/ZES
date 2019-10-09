using System;
using System.Threading;
using System.Threading.Tasks;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Dataflow;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    /// <inheritdoc />
    public abstract partial class Projection<TState> : IProjection<TState>
    {
        /// <inheritdoc />
        public class Dispatcher : ParallelDataDispatcherWithOutput<string, IStream, int>
        {
            private readonly Slice.Builder _streamFlow;

            private readonly Projection<TState> _projection;
            private readonly CancellationTokenSource _cancellation;
            private readonly Task _start;

            private Dispatcher(
                DataflowOptions options,
                Projection<TState> projection, 
                CancellationTokenSource cancel,
                Task delay,
                Slice.Builder streamFlow,
                ILog log)
                : base(projection.Key, options, cancel.Token, projection.GetType())
            {
                _projection = projection;
                _streamFlow = streamFlow;
                _cancellation = cancel;
                _start = delay;
                _streamFlow = streamFlow;
                Log = log;

                // _cancellation.Token.Register(base.Complete);
            }

            /// <inheritdoc />
            protected override Dataflow<IStream, int> CreateChildFlow(string dispatchKey)
            {
                return _streamFlow
                    .WithOptions(DataflowOptions)
                    .WithCancellation(_cancellation.Token)
                    .DelayUntil(_start)
                    .Bind(_projection);
            }

            /// <inheritdoc />
            protected override void CleanUp(Exception dataflowException)
            {
                Log?.Errors.Add(dataflowException);
            }

            /// <inheritdoc />
            public class Builder : FluentBuilder
            {
                private readonly Slice.Builder _streamFlow;
                private readonly ILog _log;

                private DataflowOptions _options = DataflowOptions.Default;
                private CancellationTokenSource _cancellation = new CancellationTokenSource();
                private Task _delayUntil;

                /// <summary>
                /// Initializes a new instance of the <see cref="Builder"/> class.
                /// </summary>
                /// <param name="streamFlow">Stream flow</param>
                /// <param name="log">Log helper</param>
                public Builder(Slice.Builder streamFlow, ILog log)
                {
                    _streamFlow = streamFlow;
                    _log = log;
                }

                internal Builder WithOptions(DataflowOptions options) =>
                    Clone(this, b => b._options = options);

                internal Builder WithCancellation(CancellationTokenSource source) =>
                    Clone(this, b => b._cancellation = source);

                internal Builder DelayUntil(Task delay) =>
                    Clone(this, b => b._delayUntil = delay);

                internal ParallelDataDispatcherWithOutput<string, IStream, int> Bind(Projection<TState> projection)
                {
                    if (projection == null)
                        throw new ArgumentNullException();
                    
                    return new Dispatcher(_options, projection, _cancellation, _delayUntil, _streamFlow, _log);
                }
            }
        }
    }
}