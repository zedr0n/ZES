using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain
{
    public class Root : EventSourced, IAggregate
    {
        public Root()
        {
            Register<RootCreated>(ApplyEvent);
        }

        public Root(string id) : this()
        {
            base.When(new RootCreated(id));    
        }

        private void ApplyEvent(RootCreated e)
        {
            Id = e.RootId;
        }
    }
}