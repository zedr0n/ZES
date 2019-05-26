using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuickGraph;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Causality
{
    /// <inheritdoc />
    public class CausalityGraph : ICausalityGraph
    {
        private readonly IStreamStore _streamStore;
        private readonly ISerializer<IEvent> _serializer;
        
        private readonly BidirectionalGraph<CausalityVertex, SEdge<CausalityVertex>> _graph 
            = new BidirectionalGraph<CausalityVertex, SEdge<CausalityVertex>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CausalityGraph"/> class.
        /// </summary>
        public CausalityGraph(IStreamStore streamStore, ISerializer<IEvent> serializer)
        {
            _streamStore = streamStore;
            _serializer = serializer;
            _streamStore.SubscribeToAll(Position.Start - 1, MessageReceived);
        }

        private async Task MessageReceived(
            IAllStreamSubscription subscription,
            StreamMessage streamMessage,
            CancellationToken cancellationToken)
        {
            var metadata = _serializer.DecodeMetadata(streamMessage.JsonMetadata);
            var vertex = new CausalityVertex(streamMessage.MessageId, streamMessage.StreamId);
            _graph.AddVertex(vertex);

            if (metadata.AncestorId != Guid.Empty)
            {
                var ancestor = _graph.Vertices.SingleOrDefault(s => s.MessageId == metadata.AncestorId);
                var edge = new SEdge<CausalityVertex>(ancestor, vertex);
                _graph.AddEdge(edge);
            }
        }

        public IEnumerable<Guid> GetCauses(Guid messageId)
        { 
            var causes = new List<Guid>();
            
            ProcessCauses(messageId, causes);
            return causes;
        }

        public IEnumerable<Guid> GetDependents(Guid messageId)
        {
            var dependents = new List<Guid>();
            
            ProcessDependents(messageId, dependents);
            return dependents;
        }

        private void ProcessDependents(Guid messageId, ICollection<Guid> dependents)
        {
            var vertex = _graph.Vertices.Single(s => s.MessageId == messageId);
            if (vertex == null)
                return;

            var edges = _graph.OutEdges(vertex).ToList();
            foreach (var edge in edges)
            {
                var id = edge.Target.MessageId;
                dependents.Add(id);
                ProcessDependents(id, dependents);
            }
        }

        private void ProcessCauses(Guid messageId, ICollection<Guid> causes)
        {
            var vertex = _graph.Vertices.SingleOrDefault(s => s.MessageId == messageId);
            if (vertex == null)
                return;
            
            var edges = _graph.InEdges(vertex).ToList();
            foreach (var edge in edges)
            {
                var ancestorId = edge.Source.MessageId;
                causes.Add(ancestorId);
                ProcessCauses(ancestorId, causes);
            }
        }

        private class CausalityVertex
        {
            public CausalityVertex(Guid messageId, string stream)
            {
                MessageId = messageId;
                Stream = stream;
            }
            
            public Guid MessageId { get; }
            public string Stream { get; }
        }
    }
}