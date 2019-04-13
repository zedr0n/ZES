using System;

namespace ZES.Interfaces.Sagas
{
    public interface ISagaRegistry
    {
        Func<IEvent, string> SagaId<TSaga>();
    }
}