using System;
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
    public class VReadGraph : IReadGraph
    {
        private readonly Lazy<Graph> _graph;

        private readonly BehaviorSubject<GraphReadState> _state = new BehaviorSubject<GraphReadState>(GraphReadState.Sleeping);
        private Flow _flow;

        private int _reading;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="VReadGraph"/> class.
        /// </summary>
        public VReadGraph()
        {
            State = _state.DistinctUntilChanged();
            
            _graph = new Lazy<Graph>(() =>
            {
                var session = new SessionNoServerShared(SystemDir);
                session.BeginRead();
                return Graph.Open(session);
            });
            
            _flow = new Flow(this);
            Start().Wait();
        }

        /// <inheritdoc />
        public IObservable<GraphReadState> State { get; }

        /// <inheritdoc />
        public async Task Start()
        {
            _flow.Start();
        }

        /// <inheritdoc />
        public async Task<long> Size() => await Execute(SizeInt);

        /// <inheritdoc />
        public void Export(string path)
        {
            _graph?.Value.ExportToGraphJson(path);
        }

        /// <inheritdoc />
        public async Task Pause()
        {
            _flow.Paused = true;
            var nextFlow = new Flow(this);
            
            // linking blocks directly as otherwise nextFlow will also complete
            _flow.OutputBlock.LinkTo(nextFlow.InputBlock);

            var flow = _flow;
            _flow = nextFlow;
            await flow.SignalAndWaitForCompletionAsync();
            
            // _state.OnNext(GraphReadState.Paused);
        }

        /// <inheritdoc />
        public async Task<int> GetStreamVersion(string key) => await Execute(() => GetStreamVersionInt(key));
        
        private int GetStreamVersionInt(string key)
        {
            var version = ExpectedVersion.NoStream;

            var g = _graph.Value; 
                
            var stream = g.FindVertexType(StreamVertex).FindProperty(StreamKeyProperty).GetPropertyVertex(key);
            if (stream == null) 
                return version;
                
            var vertex = stream.End(g.FindEdgeType(StreamEdge));
            version = (int)vertex.GetProperty(VersionProperty);

            return version;
        }

        private async Task<T> Execute<T>(Func<T> query)
        {
            var trackedQuery = new Tracked<Func<object>, object>(() => query());
            await _flow.SendAsync(trackedQuery);

            return (T)await trackedQuery.Task;
        }

        private long SizeInt()
        {
            var g = _graph.Value;
            var total = g.CountVertices();
            var streamCount = g.FindVertexType(StreamVertex).CountVertices();
            return total - streamCount;
        }

        private class Flow : Dataflow<Tracked<Func<object>, object>, Tracked<Func<object>, object>>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Flow"/> class.
            /// </summary>
            /// <param name="graph">Read graph object</param>
            public Flow(VReadGraph graph)
                : base(DataflowOptions.Default)
            { 
                var inputBlock = new TransformBlock<Tracked<Func<object>, object>, Tracked<Func<object>, object>>(x =>
                {
                    Interlocked.Increment(ref graph._reading);
                    graph._state.OnNext(GraphReadState.Queued);
                    return x;
                });
                
                var actionBlock = new TransformBlock<Tracked<Func<object>, object>,
                    bool>(
                    x =>
                    {
                        graph._state.OnNext(GraphReadState.Reading);

                        var res = x.Value();

                        x.SetResult(res);
                        return true;
                    },
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Configuration.ThreadsPerInstance });
                
                var finalBlock = new ActionBlock<bool>(b =>
                {
                    if (Interlocked.Decrement(ref graph._reading) == 0)
                        graph._state.OnNext(GraphReadState.Sleeping);
                });

                var outputBlock = new TransformBlock<Tracked<Func<object>, object>, Tracked<Func<object>, object>>(x =>
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

            public override ITargetBlock<Tracked<Func<object>, object>> InputBlock { get; }
            public override ISourceBlock<Tracked<Func<object>, object>> OutputBlock { get; }
        }
    }
}