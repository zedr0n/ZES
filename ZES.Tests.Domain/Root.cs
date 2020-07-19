using ZES.Infrastructure.Domain;
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

        private void ApplyEvent(RootUpdated e)
        {
            UpdatedAt = e.Timestamp;
        }
        
        private void ApplyEvent(RootCreated e)
        {
            Id = e.RootId;
        }
    }
}