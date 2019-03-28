using ZES.Infrastructure;
using ZES.Infrastructure.Sagas;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;

namespace ZES.Tests.Domain
{
    public class TestSagaHandler : SagaHandler<TestSaga>
    {
        public TestSagaHandler(IMessageQueue messageQueue, ISagaRepository repository, ILog logger) : base(messageQueue,repository, logger)
        {
            Register<RootCreated>(e => e.RootId);
        }
    }
}