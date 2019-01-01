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

        //private async void Handle(RootCreated e)
        //{
        //    var saga = await _repository.GetOrAdd<TestSaga>(e.RootId);
        //    saga.When(e);
        //    await _repository.Save(saga);
        //}
        
    }
}