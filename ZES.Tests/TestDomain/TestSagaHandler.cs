using ZES.Infrastructure.Sagas;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.Tests.TestDomain
{
    public class TestSagaHandler : SagaHandler<TestSaga>
    {
        public TestSagaHandler(IMessageQueue messageQueue, IDomainRepository repository) : base(messageQueue,repository)
        {
            Register<RootCreated>(e => e.RootId);
        }
    }
}