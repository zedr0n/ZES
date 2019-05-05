using ZES.Infrastructure.Domain;
using ZES.Interfaces;
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
            When(new RootCreated(id));    
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