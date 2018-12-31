using ZES.Core.Domain;
using ZES.Interfaces;

namespace ZES.Tests.TestDomain
{
    public class Root : EventSourced, IAggregate
    {
        public Root()
        {
            Register<RootCreated>(ApplyEvent);
        }

        public Root(string id) : this()
        {
            When(new RootCreated(id));    
        }

        private void ApplyEvent(RootCreated e)
        {
            Id = e.RootId;
        }
    }
}