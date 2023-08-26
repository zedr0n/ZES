using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Serialization;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Serialization;

#pragma warning disable SA1600

namespace ZES.Persistence.SQLStreamStore
{
    /// <inheritdoc />
    public class Graph : IGraph
    {
        private readonly IStreamStore _store;
        private readonly ISerializer<IEvent> _serializer;

        private readonly BidirectionalGraph<ICVertex, QEdge<ICVertex>> _graph
            = new BidirectionalGraph<ICVertex, QEdge<ICVertex>>();

        private readonly BehaviorSubject<long> _position = new BehaviorSubject<long>(-1);

        /// <summary>
        /// Initializes a new instance of the <see cref="Graph"/> class.
        /// </summary>
        /// <param name="store">Stream store</param>
        /// <param name="serializer">Metadata serializer</param>
        public Graph(IStreamStore store, ISerializer<IEvent> serializer)
        {
            _store = store;
            _serializer = serializer;
            Task.Run(() => store.SubscribeToAll(Position.Start - 1, MessageReceived));
        }
        
        private interface ICVertex
        {
            [XmlIgnore]
            string Id { get; }
            [XmlAttribute("Label")]
            string Label { get; }
            [XmlAttribute("Kind")]
            string Kind { get; }
            [XmlAttribute("Hash")]
            string MerkleHash { get; }
            [XmlAttribute("Timestamp")]
            string Timestamp { get; }
        }

        /// <inheritdoc />
        public async Task Wait()
        {
            var position = await _store.ReadHeadPosition();
            try
            {
                await _position.Timeout(Configuration.Timeout).FirstAsync(p => p == position);
            }
            catch
            {
                await Populate();
            }
        }

        /// <inheritdoc/>
        public async Task Populate()
        {
            _graph.Clear();

            await PopulateStreams();
            await PopulateEvents();
        }

        /// <inheritdoc />
        public async Task Serialise(string filename = "streams")
        {
            await Wait();
            _graph.SerializeToGraphML<ICVertex, QEdge<ICVertex>, BidirectionalGraph<ICVertex,  QEdge<ICVertex>>>(filename + ".graphml");
        }

        /// <inheritdoc />
        public long GetTimestamp(string key, int version)
        {
            var stream = _graph.Vertices.OfType<StreamVertex>().SingleOrDefault(v => v.Key == key);
            if (stream == null)
                return default;

            var events = new List<ICVertex>();
            GetDependents<StreamEdge>(stream, events);
            var e = events.OfType<EventVertex>().LastOrDefault(v => v.Version <= version);
            if (e == null)
                return default;

            DateTimeOffset.TryParse(e.Timestamp, out var timestamp);
            return timestamp.ToUnixTimeMilliseconds();
        }

        /// <inheritdoc />
        public async Task DeleteStream(string key)
        {
            await Wait();
            var vertex = _graph.Vertices.OfType<StreamVertex>().SingleOrDefault(v => v.Key == key);
            if (vertex == null)
                return;

            var dependents = GetDependents<StreamEdge>(vertex).OfType<EventVertex>().ToList();
            var final = dependents.Last();
            var path = GetPath<StreamEdge>(vertex, final);

            foreach (var e in path)
                _graph.RemoveEdge(e);

            foreach (var v in dependents.Where( d => !_graph.InEdges(d).OfType<StreamEdge>().Any()))
                _graph.RemoveVertex(v);
            
            _graph.RemoveVertex(vertex);
        }

        /// <inheritdoc />
        public async Task TrimStream(string key, int version)
        {
            await Wait();
            var streamVertex = _graph.Vertices.OfType<StreamVertex>().SingleOrDefault(v => v.Key == key);
            if (streamVertex == null)
                return;

            var dependents = GetDependents<StreamEdge>(streamVertex).OfType<EventVertex>().ToList();

            var source = dependents.SingleOrDefault(v => v.Version == version) as ICVertex;
            if (source == null && version < dependents.Max(v => v.Version))
                source = streamVertex; // _graph.OutEdges(streamVertex).OfType<StreamEdge>().Select(e => e.Target).OfType<EventVertex>().SingleOrDefault();
                
            var final = dependents.Last();
            var path = GetPath<StreamEdge>(source, final).ToList();
            var vertices = path.Select(e => e.Target);
            foreach (var e in path)
                _graph.RemoveEdge(e);

            path = GetPath<CausalityEdge>(source, final).ToList();
            foreach (var e in path)
                _graph.RemoveEdge(e);

            foreach (var v in vertices.Where(v => !_graph.InEdges(v).OfType<StreamEdge>().Any()))
                _graph.RemoveVertex(v);
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
            if (key.Contains("Command") || key.StartsWith("$")) // || key.Contains("Saga"))
                return null;
            
            var vertex = _graph.Vertices.OfType<StreamVertex>().SingleOrDefault(v => v.Key == key);

            if (vertex != null)
                return vertex;
            
            var stream = await _store.GetStream(key, _serializer);
            vertex = new StreamVertex(stream.Key, stream.Version);
            _graph.AddVertex(vertex);
                    
            if (stream.Parent != null)
            {
                vertex.Parent = stream.Parent.Key;
                vertex.ParentVersion = stream.Parent.Version;
                var source = _graph.Vertices.OfType<StreamVertex>()
                    .SingleOrDefault(v => v.Key == stream.Parent.Key);

                if (source == null)
                {
                    source = new StreamVertex(stream.Parent.Key, stream.Parent.Version);
                    _graph.AddVertex(source);
                }

                // LinkStream(source, vertex);
                // AddEdge(new StreamEdge(source, vertex));
                /* if (source.Key != vertex.Key)
                {
                    var edge = new StreamEdge(source, vertex);
                    _graph.AddEdge(edge);
                }*/
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
                        AddCommand(m);
                    else
                        await AddEvent(m);
                }
                
                if (eventsPage.IsEnd)
                    break;
                eventsPage = await eventsPage.ReadNext();
            }
        }

        private bool AddEdge<TEdge>(TEdge edge)
            where TEdge : QEdge<ICVertex>
        {
            if (edge.Source == null || edge.Target == null)
                return false;
            
            if (_graph.Edges.OfType<TEdge>().Any(e => e.Same(edge)))
                return false;

            if (edge.Source.Id == edge.Target.Id)
                return false;

            _graph.AddEdge(edge);
            return true;
        }

        private bool LinkStream(StreamVertex source, ICVertex target) => AddEdge(new StreamEdge(source, target));
        private bool LinkStream(EventVertex source, EventVertex target) => AddEdge(new StreamEdge(source, target));

        private bool LinkCause(CausalityVertex source, CausalityVertex target)
        {
            if (source?.Kind != GraphKind.Command)
                return AddEdge(new CausalityEdge(source, target));

            return AddEdge(new CommandEdge(source, target));
        }

        private async Task AddEvent(StreamMessage m)
        {
            /* if (m.StreamId.Contains("Saga"))
                return;*/
            
            var metadata = _serializer.DecodeMetadata(m.JsonMetadata);

            var streamVertex = await AddStream(m.StreamId); 
            var vertex = new EventVertex(new MessageId(m.Type, m.MessageId), metadata.AncestorId, metadata.MessageType, m.StreamId, metadata.Version, metadata.Timestamp.ToUnixTimeMilliseconds());
            _graph.AddVertex(vertex);

            var previousInStream = _graph.Vertices.OfType<EventVertex>().SingleOrDefault(s =>
                s.StreamKey == m.StreamId && s.Version == metadata.Version - 1);
            
            LinkCause(previousInStream, vertex);
            if (m.StreamVersion - await _store.DeletedCount(m.StreamId) > 0)
                LinkStream(previousInStream, vertex);
            else
                LinkStream(streamVertex, vertex);

            var dependents = _graph.Vertices.OfType<EventVertex>().Where(s => s.AncestorId.Id == m.MessageId);
            foreach (var d in dependents)
                LinkCause(vertex, d);

            var ancestors = _graph.Vertices.OfType<CausalityVertex>().Where(s => s.MessageId == metadata.AncestorId);
            foreach (var a in ancestors)
                LinkCause(a, vertex);

            vertex.MerkleHash = metadata.ContentHash;
            streamVertex.MerkleHash = CalculateStreamHash(streamVertex);
        }
        
        private void AddCommand(StreamMessage m)
        {
            var metadata = _serializer.DecodeMetadata(m.JsonMetadata);

            var vertex = new CommandVertex(metadata.MessageId, metadata.AncestorId, metadata.MessageType, m.StreamId, metadata.Timestamp.ToUnixTimeMilliseconds());
            _graph.AddVertex(vertex);

            var dependents = _graph.Vertices.OfType<CausalityVertex>().Where(v => v.AncestorId.Id == m.MessageId);
            foreach (var d in dependents)
                LinkCause(vertex, d);

            var ancestors = _graph.Vertices.OfType<CausalityVertex>().Where(v => v.MessageId == metadata.AncestorId);
            foreach (var a in ancestors)
                LinkCause(a, vertex);
        }
        
        private async Task MessageReceived(
            IAllStreamSubscription subscription,
            StreamMessage streamMessage,
            CancellationToken cancellationToken)
        {
            if (!streamMessage.StreamId.Contains("metadata") && !streamMessage.StreamId.Contains("$"))
            {
                if (streamMessage.StreamId.Contains("Command"))
                    AddCommand(streamMessage);
                else
                    await AddEvent(streamMessage);
            }
            
            _position.OnNext(streamMessage.Position);
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

        private ICollection<ICVertex> GetDependents<TEdge>(ICVertex vertex)
            where TEdge : IEdge<ICVertex>
        {
            var dependents = new List<ICVertex>();
            GetDependents<TEdge>(vertex, dependents);
            return dependents;
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

        private IEnumerable<QEdge<ICVertex>> GetPath<TEdge>(ICVertex source, ICVertex target)
            where TEdge : IEdge<ICVertex>
        {
            if (source == null || target == null)
                return new List<QEdge<ICVertex>>();
            var paths = _graph.RankedShortestPathHoffmanPavley(
                edge => edge is TEdge ? 0.0 : 100,
                source,
                target,
                1);
            
            return paths.FirstOrDefault(p => p.All(e => e is TEdge)) ?? new List<QEdge<ICVertex>>();
        }
        
        private static class GraphKind
        {
            public static string Causality => "Causes";
            public static string Command => "Command";
            public static string Stream => "Stream";
            public static string Event => "Event";
        }

        [Serializable]
        private abstract class QEdge<TVertex> : IEdge<TVertex>
            where TVertex : ICVertex 
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
            
            public bool Same(IEdge<TVertex> other)
            {
                if (other == null)
                    return false;

                return Source.Id == other.Source.Id && Target.Id == other.Target.Id;
            }
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
        [DebuggerDisplay("{Label}")]
        private class StreamVertex : ICVertex 
        {
            public StreamVertex(string key, int version = ExpectedVersion.NoStream)
            {
                Key = key;
                Version = version;
            }
            
            public string Parent { get; set; }
            public int ParentVersion { get; set; }

            public string MerkleHash { get; set; }
            public string Timestamp => DateTime.Now.ToUniversalTime()
                .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

            public string Id => Key;
            public string Label => Key; 
            public string Kind => GraphKind.Stream;
            public string Key { get; }
            public int Version { get; set; }
        }

        [Serializable]
        [DebuggerDisplay("{Label}")]
        private abstract class CausalityVertex : ICVertex
        {
            public CausalityVertex(MessageId messageId, MessageId ancestorId, long timestamp, string streamKey)
            {
                MessageId = messageId;
                AncestorId = ancestorId;
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime
                    .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
                StreamKey = streamKey;
            }
                
            public MessageId MessageId { get; }
            public MessageId AncestorId { get; }
            public string MerkleHash { get; set; }
            public string StreamKey { get; }
            public string Id => $"{StreamKey}:{MessageId.ToString()}";
            public abstract string Label { get; }
            public virtual string Kind => GraphKind.Causality;
            public string Timestamp { get; }
        }

        [Serializable]
        [DebuggerDisplay("{Label}")]
        private class EventVertex : CausalityVertex
        {
            private string _prefix = string.Empty;
            public EventVertex(MessageId messageId, MessageId ancestorId, string eventType, string streamKey, int version, long timestamp)
                : base(messageId, ancestorId, timestamp, streamKey)
            {
                SagaEvent = streamKey.Contains("Saga");
                if (SagaEvent)
                    _prefix = "Saga:";
                EventType = eventType;
                Version = version;
            }

            public int Version { get; }
            public string EventType { get; }
            public bool SagaEvent { get; }
            public override string Label => $"{_prefix}{EventType}@{Version}";
            public override string Kind => GraphKind.Event;
        }

        [Serializable]
        [DebuggerDisplay("{Label}")]
        private class CommandVertex : CausalityVertex
        {
            public CommandVertex(MessageId messageId, MessageId ancestorId, string commandType, string streamKey, long timestamp)
                : base(messageId, ancestorId, timestamp, streamKey)
            {
                CommandType = commandType;
            }

            public string CommandType { get; }
            public override string Label => CommandType;
            public override string Kind => GraphKind.Command;
        }
    }
}