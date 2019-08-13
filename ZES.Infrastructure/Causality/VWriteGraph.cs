using System;
using System.IO;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using VelocityDb.Session;
using VelocityGraph;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using static ZES.Infrastructure.Configuration.Graph;

namespace ZES.Infrastructure.Causality
{
    public interface IWriteGraph
    {
        Task Pause(int ms);
        Task AddEvent(IEvent e);

        /// <summary>
        /// Create graph schema
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task Initialize();
    }

    public class VWriteGraph : IWriteGraph
    {
        private readonly IReadOnlyGraph _readOnlyGraph;

        private readonly ActionBlock<(TaskCompletionSource<bool>,Action)> _transactions;

        public VWriteGraph(IReadOnlyGraph readOnlyGraph)
        {
            _readOnlyGraph = readOnlyGraph;
            
            _transactions = new ActionBlock<(TaskCompletionSource<bool> t, Action a)>(async x =>
            {
                await _readOnlyGraph.Pause();
                x.a();
                _readOnlyGraph.Start();
                x.t.SetResult(true);
            });
        }
        
        /// <summary>
        /// Create graph schema
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task Initialize()
        {
            using (var session = new SessionNoServerShared(SystemDir))
            {
                if (Directory.Exists(session.SystemDirectory))
                    Directory.Delete(session.SystemDirectory, true);

                session.BeginUpdate();
                File.Copy(@"../../../../../4.odb", session.SystemDirectory + "/4.odb", true);
                
                var g = new Graph(session);
                session.Persist(g);

                var streamType = g.NewVertexType(StreamVertexType);
                g.NewVertexProperty(streamType, StreamKey, DataType.String, PropertyKind.Indexed);
                
                var eventType = g.NewVertexType(VertexEventType);
                g.NewVertexProperty(eventType, VertexMessageId, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, VertexMerkleHash, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, VertexVersion, DataType.Integer, PropertyKind.Indexed);

                g.NewEdgeType(Configuration.Graph.EdgeType, false);
                g.NewEdgeType(EdgeCommandType, false);
                g.NewEdgeType(EdgeStreamType, true);
                
                session.Commit();
            }
        }
        
        private void AddEventInt(IEvent e)
        {
            using (var session = new SessionNoServerShared(SystemDir))
            {
                session.BeginUpdate();
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
                
                session.Commit();
            }
        }

        public async Task Pause(int ms)
        {
            var tsc = new TaskCompletionSource<bool>();
            await _transactions.SendAsync((tsc, () => Thread.Sleep(ms)));
            await tsc.Task;
        }
        
        public async Task AddEvent(IEvent e)
        {
            var tsc = new TaskCompletionSource<bool>();
            await _transactions.SendAsync((tsc, () => AddEventInt(e)));
            await tsc.Task;
        }
    }
}