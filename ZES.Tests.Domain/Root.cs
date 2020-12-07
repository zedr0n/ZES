using System.Collections.Generic;
using System.Linq;
using NodaTime;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain
{
    public sealed class Root : AggregateRoot, IAggregate
    {
        public Root()
        {
            Register<RootCreated>(ApplyEvent);
            Register<RootUpdated>(ApplyEvent);
            Register<SnapshotEvent>(ApplyEvent);
            Register<RootDetailsAdded>(ApplyEvent);
        }

        public Root(string id)
            : this()
        {
            Id = id;
            When(new RootCreated(id, Type.Ordinary));    
        }

        public enum Type
        {
            /// <summary>
            /// Default root
            /// </summary>
            Ordinary,
            
            /// <summary>
            /// Special root
            /// </summary>
            Special,
        }

        public List<string> Details { get; set; }
        
        public Instant UpdatedAt { get; private set; }

        public void Update()
        {
            When(new RootUpdated());
        }

        public void AddDetails(string[] details)
        {
            When(new RootDetailsAdded(details));
        }

        /// <inheritdoc/>
        protected override ISnapshotEvent CreateSnapshot() => new SnapshotEvent(UpdatedAt, Id);

        private void ApplyEvent(RootUpdated e)
        {
            UpdatedAt = e.Timestamp;
        }
        
        private void ApplyEvent(RootCreated e)
        {
            Id = e.RootId;
        }

        private void ApplyEvent(SnapshotEvent e)
        {
            Id = e.Id;
            UpdatedAt = e.UpdatedAt;
        }
        
        private void ApplyEvent(RootDetailsAdded e)
        {
            Details = e.Details.ToList();
        }

        private class SnapshotEvent : Event, ISnapshotEvent<Root>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SnapshotEvent"/> class.
            /// </summary>
            /// <param name="updatedAt">Updated at property</param>
            public SnapshotEvent(Instant updatedAt, string rootId)
            {
                UpdatedAt = updatedAt;
                Id = rootId;
            }

            public Instant UpdatedAt { get; }
            public string Id { get; set; }
        }
    }
}