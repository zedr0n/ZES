using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Domain
{
    public class DomainRepository : EsRepository<IAggregate>, IDomainRepository
    {
        public DomainRepository(IEventStore<IAggregate> eventStore, IStreamLocator<IAggregate> streams, ITimeline timeline, IBus bus)
            : base(eventStore, streams, timeline, bus)
        {
        }
    }
}