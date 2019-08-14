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
    /// <inheritdoc />
    public class VUpdateGraph : IUpdateGraph
    {
        private readonly ActionBlock<(TaskCompletionSource<bool>, Action)> _transactions;

        /// <summary>
        /// Initializes a new instance of the <see cref="VUpdateGraph"/> class.
        /// </summary>
        /// <param name="readOnlyGraph">Read graph interface</param>
        public VUpdateGraph(IReadOnlyGraph readOnlyGraph)
        {
            _transactions = new ActionBlock<(TaskCompletionSource<bool> t, Action a)>(async x =>
            {
                await readOnlyGraph.Pause();
                x.a();
                readOnlyGraph.Start();
                x.t.SetResult(true);
            });
        }

        /// <inheritdoc />
        public void Initialize()
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

                g.NewEdgeType(EdgeCommandType, false);
                g.NewEdgeType(EdgeStreamType, true);
                
                session.Commit();
            }
        }

        /// <inheritdoc />
        public async Task Pause(int ms) => await Execute(() => Thread.Sleep(ms));

        /// <inheritdoc />
        public async Task AddEvent(IEvent e) => await Execute(() => AddEventInt(e));

        private async Task Execute(Action command)
        {
            var tsc = new TaskCompletionSource<bool>();
            await _transactions.SendAsync((tsc, command));
            await tsc.Task;
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
    }
}