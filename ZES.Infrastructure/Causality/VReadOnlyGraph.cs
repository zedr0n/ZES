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
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using static ZES.Infrastructure.Configuration.Graph;

namespace ZES.Infrastructure.Causality
{
    public interface IReadOnlyGraph
    {
        void Start();
        IObservable<GraphReadState> State { get; }
        Task Pause();
        Task<int> GetStreamVersion(string key);
    }

    /// <summary>
    /// Read-only graph with persistent session
    /// </summary>
    public class VReadOnlyGraph : IReadOnlyGraph
    {
        private readonly Lazy<SessionBase> _session;
        private readonly Lazy<Graph> _graph;
        private readonly ILog _log;
        
        private readonly BehaviorSubject<GraphReadState> _state = new BehaviorSubject<GraphReadState>(GraphReadState.Sleeping);
        private Flow _flow;

        private BufferBlock<(TaskCompletionSource<object> t, Func<object> f)> _buffer;

        private TransformBlock<(TaskCompletionSource<object> t, Func<object> f), (TaskCompletionSource<object> t,
            Func<object> f)> _readBlock;
        private ActionBlock<(TaskCompletionSource<object> t, Func<object> f)> _transactions;

        private int _reading;

        private class Flow : Dataflow<(TaskCompletionSource<object> t, Func<object> f),
            (TaskCompletionSource<object> t, Func<object> f)>
        {
            private TransformBlock<(TaskCompletionSource<object> t, Func<object> f), bool> _actionBlock;
            private TransformBlock<(TaskCompletionSource<object> t, Func<object> f), (TaskCompletionSource<object> t, Func<object> f)> _inputBlock;
            private ActionBlock<bool> _finalBlock;
            private TransformBlock<(TaskCompletionSource<object> t, Func<object> f), (TaskCompletionSource<object> t, Func<object> f)> _outputBlock;
            public Action Start { get; }

            public bool Paused { get; set; } = false;

            public Flow(VReadOnlyGraph graph)
                : base(DataflowOptions.Default)
            {
                _inputBlock = new TransformBlock<(TaskCompletionSource<object> t, Func<object> f),
                    (TaskCompletionSource<object> t, Func<object> f)>(x =>
                {
                    Interlocked.Increment(ref graph._reading);
                    graph._state.OnNext(GraphReadState.Queued);
                    return x;
                });
                _actionBlock = new TransformBlock<(TaskCompletionSource<object> t, Func<object> f),
                    bool>(
                    x =>
                    {
                        graph._state.OnNext(GraphReadState.Reading);

                        var res = x.f();

                        x.t.SetResult(res);
                        return true;
                    },
                    new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = Configuration.ThreadsPerInstance });
                _finalBlock = new ActionBlock<bool>(b =>
                {
                    if (Interlocked.Decrement(ref graph._reading) == 0)
                        graph._state.OnNext(GraphReadState.Sleeping);
                });

                _outputBlock = new TransformBlock<(TaskCompletionSource<object> t, Func<object> f),
                    (TaskCompletionSource<object> t, Func<object> f)>(x =>
                {
                    Interlocked.Decrement(ref graph._reading);
                    return x;
                });

            _actionBlock.LinkTo(_finalBlock, new DataflowLinkOptions { PropagateCompletion = true } );

                Start = () =>
                {
                    _inputBlock.LinkTo(_actionBlock, new DataflowLinkOptions { PropagateCompletion = true }, x => !Paused);
                    _inputBlock.LinkTo(_outputBlock, new DataflowLinkOptions { PropagateCompletion = true }, x => Paused);
                };

                RegisterChild(_inputBlock);
                RegisterChild(_actionBlock);
                RegisterChild(_finalBlock);
                RegisterChild(_outputBlock);
            }

            public override ITargetBlock<(TaskCompletionSource<object> t, Func<object> f)> InputBlock => _inputBlock;
            public override ISourceBlock<(TaskCompletionSource<object> t, Func<object> f)> OutputBlock => _outputBlock;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="VReadOnlyGraph"/> class.
        /// </summary>
        public VReadOnlyGraph(ILog log)
        {
            _log = log;
            State = _state.DistinctUntilChanged();
            
            _session = new Lazy<SessionBase>(() => new SessionNoServerShared(SystemDir));
            _graph = new Lazy<Graph>(() =>
            {
                _session.Value.BeginRead();
                return Graph.Open(_session.Value);
            });
            
            _flow = new Flow(this);
            Start();
        }

        public void Start() => _flow.Start();
        
        public IObservable<GraphReadState> State { get; private set; }

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

        public async Task<int> GetStreamVersion(string key)
        {
            await _state.FirstAsync(s => s != GraphReadState.Pausing);
            
            var taskCompletionSource = new TaskCompletionSource<object>();
            await _flow.SendAsync<(TaskCompletionSource<object>, Func<object>)>((taskCompletionSource,
                () => GetStreamVersionInt(key)));
            
            return (int)await taskCompletionSource.Task;
        }
    }
}