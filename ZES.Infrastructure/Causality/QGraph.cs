using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using QuickGraph;
using QuickGraph.Serialization;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Causality
{
    /// <inheritdoc />
    public class QGraph : IQGraph
    {
        private readonly IStreamStore _store;
        private readonly ISerializer<IEvent> _serializer;

        private readonly BidirectionalGraph<ICVertex, QEdge<ICVertex>> _graph
            = new BidirectionalGraph<ICVertex, QEdge<ICVertex>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="QGraph"/> class.
        /// </summary>
        /// <param name="store">Stream store</param>
        /// <param name="serializer">Metadata serializer</param>
        public QGraph(IStreamStore store, ISerializer<IEvent> serializer)
        {
            _store = store;
            _serializer = serializer;
            store.SubscribeToAll(Position.Start - 1, MessageReceived);
        }
        
        private interface ICVertex
        {
            [XmlAttribute("Label")]
            string Label { get; }
            [XmlAttribute("Kind")]
            string Kind { get; }
            [XmlAttribute("Hash")]
            string MerkleHash { get; }
            [XmlAttribute("Timestamp")]
            string Timestamp { get; }
        }

        /// <inheritdoc/>
        public async Task Populate()
        {
            _graph.Clear();

            await PopulateStreams();
            await PopulateEvents();
        }

        /// <inheritdoc />
        public void Serialise(string filename = "streams")
        {
            _graph.SerializeToGraphML<ICVertex, QEdge<ICVertex>, BidirectionalGraph<ICVertex,  QEdge<ICVertex>>>(filename + ".graphml");
        }

        private async Task PopulateStreams()
        {
            var page = await _store.ListStreams();
            while (page.StreamIds.Length > 0)
            {
                foreach (var s in page.StreamIds)
                    await AddStream(s);

                page = await page.Next();
            }
        }

        private async Task<StreamVertex> AddStream(string key)
        {
            if (key.Contains("Command") || key.StartsWith("$"))
                return null;
            
            var vertex = _graph.Vertices.OfType<StreamVertex>().SingleOrDefault(v => v.Key == key);

            if (vertex != null)
                return vertex;
            
            var stream = await _store.GetStream(key, _serializer);
            vertex = new StreamVertex(stream.Key, stream.Version);
            _graph.AddVertex(vertex);
                    
            if (stream.Parent != null)
            {
                var source = _graph.Vertices.OfType<StreamVertex>()
                    .SingleOrDefault(v => v.Key == stream.Parent.Key);

                if (source == null)
                {
                    source = new StreamVertex(stream.Parent.Key, stream.Parent.Version);
                    _graph.AddVertex(source);
                }

                if (source.Key != vertex.Key)
                {
                    var edge = new StreamEdge(source, vertex);
                    _graph.AddEdge(edge);
                }
            }

            return vertex;
        }
        
        private async Task PopulateEvents()
        {
            // read all events
            var eventsPage = await _store.ReadAllForwards(Position.Start, 100);
            while (eventsPage.Messages.Length > 0)
            {
                foreach (var m in eventsPage.Messages.Where(m => !m.StreamId.StartsWith("$")))
                {
                    if (m.StreamId.Contains("Command"))
                        await AddCommand(m);
                    else
                        await AddEvent(m);
                }
                
                if (eventsPage.IsEnd)
                    break;
                eventsPage = await eventsPage.ReadNext();
            }
        }

        private async Task AddEvent(StreamMessage m)
        {
            if (m.StreamId.Contains("Saga"))
                return;
            
            var metadata = _serializer.DecodeMetadata(m.JsonMetadata);

            var streamVertex = await AddStream(m.StreamId); 
            var vertex = new EventVertex(m.MessageId, metadata.AncestorId, metadata.MessageType, m.StreamId, metadata.Version, metadata.Timestamp);
            _graph.AddVertex(vertex);

            if (m.StreamVersion == 0)
                _graph.AddEdge(new StreamEdge(streamVertex, vertex));
            
            var previousInStream = _graph.Vertices.OfType<EventVertex>().SingleOrDefault(s =>
                s.StreamKey == m.StreamId && s.Version == metadata.Version - 1);
            if (previousInStream != null && !metadata.Idempotent)
                _graph.AddEdge(new CausalityEdge(previousInStream, vertex));
            if (m.StreamVersion > 0)
                _graph.AddEdge(new StreamEdge(previousInStream, vertex));

            if (metadata.AncestorId != Guid.Empty)
            {
                var ancestor = _graph.Vertices.OfType<EventVertex>().SingleOrDefault(s => s.MessageId == metadata.AncestorId);
                if (ancestor != null && !_graph.Edges.OfType<CausalityEdge>().Any(e => 
                        e.Source.MessageId == metadata.AncestorId && e.Target.MessageId == m.MessageId))
                    _graph.AddEdge(new CausalityEdge(ancestor, vertex)); 
            }
            
            var dependents = _graph.Vertices.OfType<EventVertex>().Where(s => s.AncestorId == m.MessageId);
            foreach (var d in dependents)
            {
                if (!_graph.Edges.OfType<CausalityEdge>().Any(e => e.Source.MessageId == m.MessageId && e.Target.MessageId == d.MessageId))
                    _graph.AddEdge(new CausalityEdge(vertex, d));
            }

            var ancestors = _graph.Vertices.OfType<CausalityVertex>().Where(s => s.MessageId == metadata.AncestorId);
            foreach (var a in ancestors)
            {
                if (!_graph.Edges.OfType<CausalityEdge>().Any(e =>
                    e.Source.MessageId == metadata.AncestorId && e.Target.MessageId == metadata.MessageId))
                {
                    if (a.Kind == GraphKind.Command)
                        _graph.AddEdge(new CommandEdge(a, vertex));
                    else
                        _graph.AddEdge(new CausalityEdge(a, vertex));
                }
            }

            vertex.MerkleHash = CalculateHash(vertex, await m.GetJsonData());
            streamVertex.MerkleHash = CalculateStreamHash(streamVertex);
        }
        
        private async Task AddCommand(StreamMessage m)
        {
            var metadata = _serializer.DecodeMetadata(m.JsonMetadata);

            var vertex = new CommandVertex(metadata.MessageId, metadata.AncestorId, metadata.MessageType, metadata.Timestamp);
            _graph.AddVertex(vertex);

            var dependents = _graph.Vertices.OfType<EventVertex>().Where(v => v.AncestorId == m.MessageId);
            foreach (var d in dependents)
            {
                if (!_graph.Edges.OfType<CommandEdge>().Any( e => e.Source?.MessageId == m.MessageId && e.Target.MessageId == d.MessageId ))
                    _graph.AddEdge(new CommandEdge(vertex, d));
            }

            var ancestors = _graph.Vertices.OfType<CausalityVertex>().Where(v => v.MessageId == metadata.AncestorId);
            foreach (var a in ancestors)
            {
                if (!_graph.Edges.OfType<CausalityEdge>().Any(e => e.Source?.MessageId == a.MessageId && e.Target?.MessageId == m.MessageId))
                    _graph.AddEdge(new CausalityEdge(a, vertex));
            }

            vertex.MerkleHash = CalculateHash(vertex, await m.GetJsonData());
        }
        
        private async Task MessageReceived(
            IAllStreamSubscription subscription,
            StreamMessage streamMessage,
            CancellationToken cancellationToken)
        {
            if (streamMessage.StreamId.Contains("metadata") || streamMessage.StreamId.Contains("$"))
                return;
            
            if (streamMessage.StreamId.Contains("Command"))
                await AddCommand(streamMessage);
            else
                await AddEvent(streamMessage);
        }

        private string CalculateHash(ICVertex vertex, string jsonData)
        {
            var causes = new List<ICVertex>();
            GetCauses<CausalityEdge>(vertex, causes);

            var aggr = jsonData + causes.OfType<CausalityVertex>().Select(v => v.MerkleHash).Aggregate(string.Empty, (c, n) => c + n);
            return Hashing.Sha256(aggr);
        }

        private string CalculateStreamHash(StreamVertex vertex)
        {
            var vertices = new List<ICVertex>();
            GetDependents<StreamEdge>(vertex, vertices);
            var hashes = vertices.OfType<EventVertex>().Select(e => e.MerkleHash);
            var hashString = hashes.Aggregate(string.Empty, (c, n) => c + n);
            return Hashing.Sha256(hashString);
        }

        private void GetCauses<TEdge>(ICVertex vertex, ICollection<ICVertex> causes)
            where TEdge : IEdge<ICVertex>
        {
            foreach (var e in _graph.InEdges(vertex).OfType<TEdge>())
            {
                causes.Add(e.Source);
                GetCauses<TEdge>(e.Source, causes);
            }
        }

        private void GetDependents<TEdge>(ICVertex vertex, ICollection<ICVertex> dependents)
            where TEdge : IEdge<ICVertex>
        {
            var edges = _graph.OutEdges(vertex).OfType<TEdge>();
            foreach (var edge in edges)
            {
                dependents.Add(edge.Target);
                GetDependents<TEdge>(edge.Target, dependents);    
            }
        }
        
        private static class GraphKind
        {
            public static string Causality => "Causes";
            public static string Command => "Command";
            public static string Stream => "Stream";
            public static string Event => "Event";
        }

        [Serializable]
        private class QEdge<TVertex> : IEdge<TVertex>
        {
            private SEdge<TVertex> _edge;

            public QEdge(TVertex source, TVertex target)
            {
                _edge = new SEdge<TVertex>(source, target);
            }

            public TVertex Source => _edge.Source;
            public TVertex Target => _edge.Target;
            [XmlAttribute("Kind")]
            public virtual string Kind { get; }
        }

        private class StreamEdge : QEdge<ICVertex>
        {
            public StreamEdge(ICVertex source, ICVertex target)
                : base(source, target)
            {
            }

            public override string Kind => GraphKind.Stream;
        }

        private class CommandEdge : QEdge<ICVertex>
        {
            public CommandEdge(ICVertex source, ICVertex target) 
                : base(source, target)
            {
            }
            
            public new CommandVertex Source => base.Source as CommandVertex;
            public new EventVertex Target => base.Target as EventVertex;

            public override string Kind => GraphKind.Command;
        }

        private class CausalityEdge : QEdge<ICVertex>, IEdge<CausalityVertex>
        {
            public CausalityEdge(ICVertex source, ICVertex target) 
                : base(source, target)
            {
            }

            public override string Kind => GraphKind.Causality;
            public new CausalityVertex Source => base.Source as CausalityVertex;
            public new CausalityVertex Target => base.Target as CausalityVertex;
        }
        
        [Serializable]
        private class StreamVertex : ICVertex 
        {
            public StreamVertex(string key, int version = ExpectedVersion.NoStream)
            {
                Key = key;
                Version = version;
            }

            public string MerkleHash { get; set; }
            public string Timestamp => DateTime.Now.ToUniversalTime()
                .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
            public string Label => $"{Key}@{Version}";
            public string Kind => GraphKind.Stream;
            public string Key { get; }
            public int Version { get; set; }
        }

        [Serializable]
        private abstract class CausalityVertex : ICVertex
        {
            public CausalityVertex(Guid messageId, Guid ancestorId, long timestamp)
            {
                MessageId = messageId;
                AncestorId = ancestorId;
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime.ToUniversalTime()
                    .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
            }
                
            public Guid MessageId { get; }
            public Guid AncestorId { get; }
            public string MerkleHash { get; set; }
            public abstract string Label { get; }
            public virtual string Kind => GraphKind.Causality;
            public string Timestamp { get; }

        }

        [Serializable]
        private class EventVertex : CausalityVertex 
        {
            public EventVertex(Guid messageId, Guid ancestorId, string eventType, string streamKey, int version, long timestamp)
                : base(messageId, ancestorId, timestamp)
            {
                EventType = eventType;
                StreamKey = streamKey;
                Version = version;
            }

            public string StreamKey { get; }
            public int Version { get; }
            public string EventType { get; }
            public override string Label => $"{EventType}@{Version}";
            public override string Kind => GraphKind.Event;
        }

        [Serializable]
        private class CommandVertex : CausalityVertex
        {
            public CommandVertex(Guid messageId, Guid ancestorId, string commandType, long timestamp)
                : base(messageId, ancestorId, timestamp)
            {
                CommandType = commandType;
            }

            public string CommandType { get; }
            public override string Label => CommandType;
            public override string Kind => GraphKind.Command;
        }
    }
}