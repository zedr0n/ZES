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
using ZES.Interfaces.EventStore;
using static ZES.Infrastructure.Configuration.Graph;

namespace ZES.Infrastructure.Causality
{
    /// <inheritdoc />
    public class VGraph : IGraph
    {
        private readonly ActionBlock<Tracked<Action>> _transactions;

        /// <summary>
        /// Initializes a new instance of the <see cref="VGraph"/> class.
        /// </summary>
        /// <param name="readGraph">Read graph interface</param>
        public VGraph(IReadGraph readGraph)
        {
            _transactions = new ActionBlock<Tracked<Action>>(async x =>
            {
                await readGraph.Pause();
                x.Value();
                readGraph.Start();
                x.Complete();
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

                var streamType = g.NewVertexType(StreamVertex);
                g.NewVertexProperty(streamType, StreamKeyProperty, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(streamType, ParentStreamProperty, DataType.String, PropertyKind.Indexed);

                var streamMetadataType = g.NewVertexType(StreamMetadataVertex);
                g.NewVertexProperty(streamMetadataType, StreamKeyProperty, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(streamMetadataType, ParentKeyProperty, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(streamMetadataType, ParentVersionProperty, DataType.Integer, PropertyKind.Indexed);
                
                var eventType = g.NewVertexType(EventVertex);
                g.NewVertexProperty(eventType, MessageIdProperty, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, MerkleHashProperty, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, VersionProperty, DataType.Integer, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, AncestorIdProperty, DataType.String, PropertyKind.Indexed);
                g.NewVertexProperty(eventType, StreamKeyProperty, DataType.String, PropertyKind.Indexed);

                var commandType = g.NewVertexType(CommandVertex);
                g.NewVertexProperty(commandType, MessageIdProperty, DataType.String, PropertyKind.Indexed);

                g.NewEdgeType(CommandEdge, true);
                g.NewEdgeType(StreamEdge, true);
                g.NewEdgeType(MetadataEdge, true);
                
                session.Commit();
            }
        }

        /// <inheritdoc />
        public async Task Pause(int ms) => await Execute(() => Thread.Sleep(ms));

        /// <inheritdoc />
        public async Task AddEvent(IEvent e) => await Execute(() => AddEventInt(e));

        /// <inheritdoc />
        public async Task AddCommand(ICommand command) => await Execute(() => AddCommandInt(command));

        /// <inheritdoc />
        public async Task AddStreamMetadata(IStream stream) => await Execute(() => AddStreamMetadataInt(stream));
        
        private async Task Execute(Action command)
        {
            var tracked = new Tracked<Action>(command);
            await _transactions.SendAsync(tracked);
            await tracked.Task;
        }

        private void AddStreamMetadataInt(IStream stream)
        {
            using (var session = new SessionNoServerShared(SystemDir))
            {
                session.BeginUpdate();
                var g = Graph.Open(session);

                var streamType = g.FindVertexType(StreamVertex);
                var streamVertex = streamType.GetVertices().SingleOrDefault(v => (string)v.GetProperty(StreamKeyProperty) == stream.Key);
                
                if (streamVertex == null)
                {
                    streamVertex = g.NewVertex(streamType);
                    streamVertex.SetProperty(streamType.FindProperty(StreamKeyProperty), stream.Key);
                }

                var metadataType = g.FindVertexType(StreamMetadataVertex);
                var vertex = g.NewVertex(metadataType);
                vertex.SetProperty(StreamKeyProperty, stream.Key);
                if (stream.Parent != null)
                {
                    vertex.SetProperty(ParentKeyProperty, stream.Parent.Key);
                    vertex.SetProperty(ParentVersionProperty, stream.Parent.Version);
                }

                var metaEdgeType = g.FindEdgeType(MetadataEdge);
                var prevMeta = streamVertex.End(g.FindEdgeType(MetadataEdge));
                
                prevMeta.AddEdge(metaEdgeType, vertex);
                session.Commit();
            }
        }

        private void AddCommandInt(ICommand command)
        {
            using (var session = new SessionNoServerShared(SystemDir))
            {
                session.BeginUpdate();
                var g = Graph.Open(session);

                var commandType = g.FindVertexType(CommandVertex);
                var vertex = g.NewVertex(commandType);
                
                vertex.SetProperty(commandType.FindProperty(MessageIdProperty), command.MessageId.ToString());

                var resultEvents = g.FindVertexType(EventVertex)
                    .GetVertices()
                    .Where(v => (string)v.GetProperty(AncestorIdProperty) == command.MessageId.ToString())
                    .ToList();
                /*var streamKey = (string)resultEvents.Select( v => v.GetProperty(StreamKeyProperty)).Distinct().SingleOrDefault();
                if (streamKey == null)
                    throw new InvalidOperationException();
                
                var streamType = g.FindVertexType(StreamVertex);
                var stream = streamType.GetVertices().SingleOrDefault(v => (string)v.GetProperty(StreamKeyProperty) == streamKey);

                var streamEdge = g.FindEdgeType(StreamEdge);
                var prevVertex = stream.End(streamEdge);
                prevVertex.AddEdge(streamEdge, vertex);*/

                var commandEdge = g.FindEdgeType(CommandEdge);
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

                var eventType = g.FindVertexType(EventVertex);
                var vertex = g.NewVertex(eventType);
                
                vertex.SetProperty(eventType.FindProperty(MessageIdProperty), e.MessageId.ToString());
                vertex.SetProperty(eventType.FindProperty(VersionProperty), e.Version);
                vertex.SetProperty(eventType.FindProperty(AncestorIdProperty), e.AncestorId.ToString());
                vertex.SetProperty(eventType.FindProperty(StreamKeyProperty), e.Stream);

                var streamType = g.FindVertexType(StreamVertex);
                var stream = streamType.FindProperty(StreamKeyProperty).GetPropertyVertex(e.Stream);

                var edgeStream = g.FindEdgeType(StreamEdge);
                
                var prevVertex = stream.End(edgeStream);
                prevVertex.AddEdge(edgeStream, vertex);
                
                session.Commit();
            }
        }
    }
}