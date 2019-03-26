using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Sagas
{
    public class SagaRepository : EsRepository<ISaga>, ISagaRepository
    {
        public SagaRepository(IEventStore<ISaga> eventStore, IStreamLocator<ISaga> streams, ITimeline timeline, IBus bus) : base(eventStore, streams, timeline, bus)
        {
        }
    }
}