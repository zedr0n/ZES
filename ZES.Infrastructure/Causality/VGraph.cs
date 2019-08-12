using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Frontenac.Blueprints;
using SqlStreamStore.Streams;
using VelocityDb.Session;
using VelocityGraph;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using EdgeType = VelocityGraph.EdgeType;
using IGraph = ZES.Interfaces.Causality.IGraph;

namespace ZES.Infrastructure.Causality
{
    public static class GraphExtensions
    {
        public static IEnumerable<Vertex> GetPath(this Vertex from, EdgeType edgeType, Vertex to = null)
        {
            var vertices = new List<Vertex> { from };

            var descendants = from.Traverse(Direction.Out, new HashSet<EdgeType> { edgeType });
            while (descendants.Count > 0)
            {
                from = descendants.FirstOrDefault().Key;
                vertices.Add(from);
                if (from.VertexId == to?.VertexId)
                    break;
                
                descendants = from.Traverse(Direction.Out, new HashSet<EdgeType> { edgeType }); 
            }

            return vertices;
        }

        public static Vertex End(this Vertex from, EdgeType edgeType)
        {
            return from.GetPath(edgeType).LastOrDefault();
        }
    }
    
    /// <summary>
    /// Event graph
    /// </summary>
    public class VGraph : IGraph
    {
        private const string VertexEventType = "Event";
        private const string StreamVertexType = "Stream";

        private const string StreamKey = "streamKey";    
        private const string VertexMessageId = "messageId";
        private const string VertexMerkleHash = "merkleHash";
        private const string VertexVersion = "version";
        private const string EdgeType = "CAUSES";
        private const string EdgeStreamType = "STREAM";
        private const string EdgeProperty = "EdgeType";
        
        private const string SystemDir = "VelocityGraph";
        
        private readonly BehaviorSubject<GraphState> _state = new BehaviorSubject<GraphState>(GraphState.Sleeping);
        private readonly BehaviorSubject<bool> _request = new BehaviorSubject<bool>(false);
        private readonly BehaviorSubject<int> _readRequestsSubject = new BehaviorSubject<int>(0);
        private readonly ILog _log;
        
        private Graph _graph;
        private SessionBase _readSession;
        private int _reading = 0;
        private int _readRequests = 0;

        private readonly object _lock = new object();

        /// <inheritdoc />
        public IObservable<GraphState> State => _state.AsObservable();

        public IObservable<int> ReadRequests => _readRequestsSubject.AsObservable();

        public VGraph(ILog log)
        {
            _log = log;
            // _request.Subscribe(b => _log.Info($"Graph update requested : {b}"));
            // _state.Subscribe(s => _log.Info($"Graph state : {s}"));
        }
        
        /// <summary>
        /// Create graph schema
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task Reinitialize()
        {
            using (var session = await BeginUpdate(true))
            {
                var g = new Graph(session);
                session.Persist(g);

                var streamType = g.NewVertexType(StreamVertexType);
                g.NewVertexProperty(streamType, StreamKey, DataType.String, PropertyKind.Indexed);
                
                var eventType = g.NewVertexType(VertexEventType);
                g.NewVertexProperty(eventType, VertexMessageId, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, VertexMerkleHash, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, VertexVersion, DataType.Integer, PropertyKind.Indexed);

                var edge = g.NewEdgeType(EdgeType, false);
                edge.NewProperty(EdgeProperty, DataType.Integer, PropertyKind.Indexed);

                g.NewEdgeType(EdgeStreamType, true);
                
                await EndUpdate(session);
            }
        }

        public async Task<int> GetStreamVersion(string key)
        {
            var version = ExpectedVersion.NoStream;
            await BeginRead();
            // Thread.Sleep(20);

            var g = _graph; 
                
            var stream = g.FindVertexType(StreamVertexType).FindProperty(StreamKey).GetPropertyVertex(key);
            if (stream == null) 
                return version;
                
            var vertex = stream.End(g.FindEdgeType(EdgeStreamType));
            version = (int) vertex.VertexType.FindProperty(VertexVersion).GetPropertyValue(vertex.VertexId);

            await EndRead();
            return version;
        }
        
        /// <summary>
        /// 
        /// </summary>
        public async void AddEvent(IEvent e)
        {
            await _state.FirstAsync(s => s == GraphState.Sleeping);
            
            using (var session = await BeginUpdate()) 
            {
                var g = Graph.Open(session);

                var eventType = g.FindVertexType(VertexEventType);
                var vertex = g.NewVertex(eventType);
                
                vertex.SetProperty(eventType.FindProperty(VertexMessageId), e.MessageId.ToString());
                vertex.SetProperty(eventType.FindProperty(VertexVersion), e.Version);

                var streamType = g.FindVertexType(StreamVertexType);
                var stream = streamType.FindProperty(StreamKey).GetPropertyVertex(e.Stream);

                var edgeStream = g.FindEdgeType(EdgeStreamType);
                
                if (stream == null)
                {
                    stream = g.NewVertex(streamType);
                    stream.SetProperty(streamType.FindProperty(StreamKey), e.Stream);
                }

                var prevVertex = stream.End(edgeStream);
                prevVertex.AddEdge(edgeStream, vertex);

                await EndUpdate(session);
            }
        }

        private async Task BeginRead()
        {
            _readRequestsSubject.OnNext(Interlocked.Increment(ref _readRequests));
            await _request.FirstAsync(b => b == false).Timeout(Configuration.Timeout);
            await State.FirstAsync(s => s == GraphState.Sleeping || s == GraphState.Reading).Timeout(Configuration.Timeout);

            // Thread.Sleep(5);
            Interlocked.Increment(ref _reading);
            _state.OnNext(GraphState.Reading);

            lock (_lock)
            {
                if (_readSession == null)
                {
                    _readSession = new SessionNoServerShared(SystemDir);  
                    _readSession.BeginRead();
                    _graph = Graph.Open(_readSession);
                }
            }
        }

        private async Task EndRead()
        {
            if (_readSession == null)
                throw new InvalidOperationException("Read session not valid");

            if ( Interlocked.Decrement(ref _reading) == 0 && await _state.FirstAsync() == GraphState.Reading)
                _state.OnNext(GraphState.Sleeping);
            // _log.Info(_reading);
            
            _readRequestsSubject.OnNext(Interlocked.Decrement(ref _readRequests));
        }

        private async Task CancelRead()
        {
            await State.FirstAsync(s => s == GraphState.Sleeping).Timeout(Configuration.Timeout);
            
            if (_readSession != null && !_readSession.IsDisposed)
            {
                _readSession.Commit();
                _readSession.Dispose();
            }

            _readSession = null;
            _graph = null;
        }

        public async Task Pause(int ms)
        {
            using (var session = await BeginUpdate())
            {
                Thread.Sleep(ms);
                await EndUpdate(session);
            }
        }

        private async Task<SessionBase> BeginUpdate(bool reset = false)
        {
            _request.OnNext(true);
            
            await CancelRead();
            await State.FirstAsync(s => s == GraphState.Sleeping).Timeout(Configuration.Timeout);
            _state.OnNext(GraphState.Updating);
            
            _request.OnNext(false);

            var session = new SessionNoServerShared(SystemDir);

            if (reset)
            {
                if (Directory.Exists(session.SystemDirectory))
                    Directory.Delete(session.SystemDirectory, true);
            }
            
            session.BeginUpdate();
            if (reset)
                File.Copy(@"../../../../../4.odb", session.SystemDirectory + "/4.odb", true);
            
            return session;
        }

        private async Task EndUpdate(SessionBase session)
        {
            await State.FirstAsync(s => s == GraphState.Updating).Timeout(Configuration.Timeout);
            
            session.Commit();
            _state.OnNext(_readRequests > 0 ? GraphState.Reading : GraphState.Sleeping);
        }
    }
}