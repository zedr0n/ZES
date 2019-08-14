using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SqlStreamStore.Streams;
using VelocityDb.Session;
using VelocityGraph;
using ZES.Interfaces.Causality;
using static ZES.Infrastructure.Configuration.Graph;

namespace ZES.Infrastructure.Causality
{
    /// <summary>
    /// Read-only graph with persistent session
    /// </summary>
    public class VReadOnlyGraph : IReadOnlyGraph
    {
        private readonly Lazy<Graph> _graph;

        private readonly BehaviorSubject<GraphReadState> _state = new BehaviorSubject<GraphReadState>(GraphReadState.Sleeping);
        private Flow _flow;

        private int _reading;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="VReadOnlyGraph"/> class.
        /// </summary>
        public VReadOnlyGraph()
        {
            State = _state.DistinctUntilChanged();
            
            var session = new Lazy<SessionBase>(() => new SessionNoServerShared(SystemDir));
            _graph = new Lazy<Graph>(() =>
            {
                session.Value.BeginRead();
                return Graph.Open(session.Value);
            });
            
            _flow = new Flow(this);
            Start();
        }

        /// <inheritdoc />
        public IObservable<GraphReadState> State { get; }

        /// <inheritdoc />
        public void Start() => _flow.Start();

        /// <inheritdoc />
        public async Task Pause()
        {
            var state = _state.Value;
 
            _state.OnNext(GraphReadState.Pausing);
            _flow.Paused = true;

            var flow = _flow;
            var nextFlow = new Flow(this);
            _flow = nextFlow;
            
            // _flow.OutputBlock.LinkTo(nextFlow.InputBlock);
            // linking blocks directly as otherwise nextFlow will also complete
            flow.OutputBlock.LinkTo(nextFlow.InputBlock);
            
            await flow.SignalAndWaitForCompletionAsync();

            // _flow = nextFlow;
            _state.OnNext(state);
        }

        /// <inheritdoc />
        public async Task<int> GetStreamVersion(string key) => await Execute(() => GetStreamVersionInt(key));
        
        private int GetStreamVersionInt(string key)
        {
            var version = ExpectedVersion.NoStream;

            var g = _graph.Value; 
                
            var stream = g.FindVertexType(StreamVertexType).FindProperty(StreamKey).GetPropertyVertex(key);
            if (stream == null) 
                return version;
                
            var vertex = stream.End(g.FindEdgeType(EdgeStreamType));
            version = (int)vertex.VertexType.FindProperty(VertexVersion).GetPropertyValue(vertex.VertexId);

            return version;
        }

        private async Task<T> Execute<T>(Func<T> query)
        {
            await _state.FirstAsync(s => s != GraphReadState.Pausing);
 
            var taskCompletionSource = new TaskCompletionSource<object>();
            await _flow.SendAsync<(TaskCompletionSource<object>, Func<object>)>((taskCompletionSource,
                () => query()));
            
            return (T)await taskCompletionSource.Task;
        }
        
        private class Flow : Dataflow<(TaskCompletionSource<object> t, Func<object> f),
            (TaskCompletionSource<object> t, Func<object> f)>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Flow"/> class.
            /// </summary>
            /// <param name="graph">Read graph object</param>
            public Flow(VReadOnlyGraph graph)
                : base(DataflowOptions.Default)
            { 
                var inputBlock = new TransformBlock<(TaskCompletionSource<object> t, Func<object> f),
                    (TaskCompletionSource<object> t, Func<object> f)>(x =>
                {
                    Interlocked.Increment(ref graph._reading);
                    graph._state.OnNext(GraphReadState.Queued);
                    return x;
                });
                
                var actionBlock = new TransformBlock<(TaskCompletionSource<object> t, Func<object> f),
                    bool>(
                    x =>
                    {
                        graph._state.OnNext(GraphReadState.Reading);

                        var res = x.f();

                        x.t.SetResult(res);
                        return true;
                    },
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Configuration.ThreadsPerInstance });
                
                var finalBlock = new ActionBlock<bool>(b =>
                {
                    if (Interlocked.Decrement(ref graph._reading) == 0)
                        graph._state.OnNext(GraphReadState.Sleeping);
                });

                var outputBlock = new TransformBlock<(TaskCompletionSource<object> t, Func<object> f),
                    (TaskCompletionSource<object> t, Func<object> f)>(x =>
                {
                    Interlocked.Decrement(ref graph._reading);
                    return x;
                });

                actionBlock.LinkTo(finalBlock, new DataflowLinkOptions { PropagateCompletion = true } );

                Start = () =>
                {
                    inputBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true }, x => !Paused);
                    inputBlock.LinkTo(outputBlock, new DataflowLinkOptions { PropagateCompletion = true }, x => Paused);
                };

                RegisterChild(inputBlock);
                RegisterChild(actionBlock);
                RegisterChild(finalBlock);
                RegisterChild(outputBlock);

                InputBlock = inputBlock;
                OutputBlock = outputBlock;
            }
            
            public Action Start { get; }

            public bool Paused { get; set; }

            public override ITargetBlock<(TaskCompletionSource<object> t, Func<object> f)> InputBlock { get; } 
            public override ISourceBlock<(TaskCompletionSource<object> t, Func<object> f)> OutputBlock { get; } 
        }
    }
}