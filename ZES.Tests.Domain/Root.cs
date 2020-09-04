using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain
{
    public sealed class Root : EventSourced, IAggregate
    {
        public Root()
        {
            Register<RootCreated>(ApplyEvent);
            Register<RootUpdated>(ApplyEvent);
            Register<SnapshotEvent>(ApplyEvent);
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
        
        public long UpdatedAt { get; private set; }

        public void Update()
        {
            When(new RootUpdated(Id));
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
            Id = e.RootId;
            UpdatedAt = e.UpdatedAt;
        }
        
        private class SnapshotEvent : Event, ISnapshotEvent
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SnapshotEvent"/> class.
            /// </summary>
            /// <param name="updatedAt">Updated at property</param>
            public SnapshotEvent(long updatedAt, string rootId)
            {
                UpdatedAt = updatedAt;
                RootId = rootId;
            }

            public long UpdatedAt { get; }
            public string RootId { get; }
        }
    }
}