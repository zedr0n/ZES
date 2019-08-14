using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using VelocityDb.Session;
using VelocityGraph;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
using static ZES.Infrastructure.Configuration.Graph;
using Stream = ZES.Infrastructure.Streams.Stream;

namespace ZES.Infrastructure.Causality
{
    /// <inheritdoc />
    public class VGraph : IGraph
    {
        private readonly ActionBlock<(TaskCompletionSource<bool>, Action)> _transactions;

        /// <summary>
        /// Initializes a new instance of the <see cref="VGraph"/> class.
        /// </summary>
        /// <param name="readGraph">Read graph interface</param>
        public VGraph(IReadGraph readGraph)
        {
            _transactions = new ActionBlock<(TaskCompletionSource<bool> t, Action a)>(async x =>
            {
                await readGraph.Pause();
                x.a();
                readGraph.Start();
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

                var streamType = g.NewVertexType(VertexStreamType);
                g.NewVertexProperty(streamType, StreamKey, DataType.String, PropertyKind.Indexed);
                
                var eventType = g.NewVertexType(VertexEventType);
                g.NewVertexProperty(eventType, VertexMessageId, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, VertexMerkleHash, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, VertexVersion, DataType.Integer, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, VertexAncestorId, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, VertexStream, DataType.String, PropertyKind.Indexed);

                var commandType = g.NewVertexType(VertexCommandType);
                g.NewVertexProperty(commandType, VertexMessageId, DataType.String, PropertyKind.Indexed);

                g.NewEdgeType(EdgeCommandType, false);
                g.NewEdgeType(EdgeStreamType, true);
                
                session.Commit();
            }
        }

        /// <inheritdoc />
        public async Task Pause(int ms) => await Execute(() => Thread.Sleep(ms));

        /// <inheritdoc />
        public async Task AddEvent(IEvent e) => await Execute(() => AddEventInt(e));

        public async Task AddCommand(ICommand command) => await Execute(() => AddCommandInt(command));

        private async Task Execute(Action command)
        {
            var tsc = new TaskCompletionSource<bool>();
            await _transactions.SendAsync((tsc, command));
            await tsc.Task;
        }

        private void AddCommandInt(ICommand command)
        {
            using (var session = new SessionNoServerShared(SystemDir))
            {
                session.BeginUpdate();
                var g = Graph.Open(session);

                var commandType = g.FindVertexType(VertexCommandType);
                var vertex = g.NewVertex(commandType);
                
                vertex.SetProperty(commandType.FindProperty(VertexMessageId), command.MessageId.ToString());

                var resultEvents = g.FindVertexType(VertexEventType)
                    .GetVertices()
                    .Where(v => (string)v.GetProperty(VertexAncestorId) == command.MessageId.ToString())
                    .ToList();
                var streamKey = (string)resultEvents.Select( v => v.GetProperty(VertexStream)).Distinct().SingleOrDefault();
                if (streamKey == null)
                    throw new InvalidOperationException();
                
                var streamType = g.FindVertexType(VertexStreamType);
                var stream = streamType.GetVertices().SingleOrDefault(v => (string)v.GetProperty(StreamKey) == streamKey);

                var streamEdge = g.FindEdgeType(EdgeStreamType);
                var prevVertex = stream.End(streamEdge);
                prevVertex.AddEdge(streamEdge, vertex);

                var commandEdge = g.FindEdgeType(EdgeCommandType);
                foreach (var e in resultEvents)
                    vertex.AddEdge(commandEdge, e);

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
                vertex.SetProperty(eventType.FindProperty(VertexAncestorId), e.AncestorId.ToString());
                vertex.SetProperty(eventType.FindProperty(VertexStream), e.Stream);

                var streamType = g.FindVertexType(VertexStreamType);
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