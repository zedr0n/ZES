using System;
using System.Threading;
using System.Threading.Tasks;
using Gridsum.DataflowEx;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    public abstract partial class Projection<TState>
    {
        public partial class ProjectionDispatcher : ParallelDataDispatcher<string, IStream, int>
        {
            private readonly StreamFlow.Builder _streamFlow;

            private readonly Projection<TState> _projection;
            private readonly CancellationTokenSource _cancellation;
            private readonly Lazy<Task> _start;

            private ProjectionDispatcher(
                DataflowOptions options,
                Projection<TState> projection, 
                CancellationTokenSource cancel,
                Lazy<Task> delay,
                StreamFlow.Builder streamFlow,
                ILog log)
                : base(projection.Key, options, projection.GetType())
            {
                _projection = projection;
                _streamFlow = streamFlow;
                _cancellation = cancel;
                _start = delay;
                _streamFlow = streamFlow;
                Log = log;
            }

            /// <inheritdoc />
            protected override Dataflow<IStream, int> CreateChildFlow(string dispatchKey)
            {
                return _streamFlow
                    .WithOptions(DataflowOptions)
                    .WithCancellation(_cancellation)
                    .DelayUntil(_start)
                    .Bind(_projection.When);
            }

            protected override void CleanUp(Exception dataflowException)
            {
                if (dataflowException != null)
                    Log?.Error($"Dataflow error : {dataflowException}");
                base.CleanUp(dataflowException);
            }

            public class Builder 
            {
                private readonly StreamFlow.Builder _streamFlow;
                private readonly ILog _log;

                private Lazy<Task> _delayUntil = new Lazy<Task>(() => Task.CompletedTask);
                private DataflowOptions _options = DataflowOptions.Default;
                private CancellationTokenSource _cancellation = new CancellationTokenSource();

                public Builder(StreamFlow.Builder streamFlow, ILog log)
                {
                    _streamFlow = streamFlow;
                    _log = log;
                }

                public Builder WithOptions(DataflowOptions options)
                {
                    _options = options;
                    return this;
                }

                public Builder WithCancellation(CancellationTokenSource source)
                {
                    _cancellation = source;
                    return this;
                }

                public Builder DelayUntil(Lazy<Task> delay)
                {
                    _delayUntil = delay;
                    return this;
                }

                public ParallelDataDispatcher<string, IStream, int> Bind(Projection<TState> projection)
                {
                    if (projection == null)
                        throw new ArgumentNullException();
                    
                    return new ProjectionDispatcher(_options, projection, _cancellation, _delayUntil, _streamFlow, _log);    
                }
            }
        }
    }
}