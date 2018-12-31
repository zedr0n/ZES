using ZES.Infrastructure.Sagas;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.Tests.TestDomain
{
    public class TestSagaHandler : SagaHandler<TestSaga>
    {
        private readonly IDomainRepository _repository;
        
        public TestSagaHandler(IMessageQueue messageQueue, IDomainRepository repository) : base(messageQueue)
        {
            _repository = repository;
            Register<RootCreated>(Apply);
        }

        private async void Apply(RootCreated e)
        {
            var saga = await _repository.GetOrAdd<TestSaga>(e.RootId);
            saga.When(e);
            await _repository.Save(saga);
        }
        
    }
}