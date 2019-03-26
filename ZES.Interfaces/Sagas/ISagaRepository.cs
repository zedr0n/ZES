using ZES.Interfaces.EventStore;

namespace ZES.Interfaces.Sagas
{
    public interface ISagaRepository : IEsRepository<ISaga>
    {

    }
}