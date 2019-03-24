using NLog;
using ZES.Infrastructure.Sagas;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.Tests.TestDomain
{
    public class TestSagaHandler : SagaHandler<TestSaga>
    {
        public TestSagaHandler(IMessageQueue messageQueue, IDomainRepository repository, ILogger _logger) : base(messageQueue,repository, _logger)
        {
            Register<RootCreated>(e => e.RootId);
        }
    }
}