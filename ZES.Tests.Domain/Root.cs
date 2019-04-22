using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain
{
    public class Root : EventSourced, IAggregate
    {
        private int _updateCount;
        public Root()
        {
            Register<RootCreated>(ApplyEvent);
            Register<RootUpdated>(ApplyEvent);
        }

        public Root(string id) : this()
        {
            base.When(new RootCreated(id));    
        }

        public void Update()
        {
            base.When(new RootUpdated(Id));
        }

        private void ApplyEvent(RootUpdated e)
        {
            _updateCount++;
        }
        
        private void ApplyEvent(RootCreated e)
        {
            Id = e.RootId;
        }
    }
}