using Stateless;
using ZES.Infrastructure.Sagas;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Sagas
{
    public class TestSaga : StatelessSaga<TestSaga.State, TestSaga.Trigger>
    {
        public enum Trigger { Created } 
        public enum State { Open, Complete}

        private string _rootId;
        
        public TestSaga()
        {
            Register<RootCreated>(Trigger.Created,When);
        }

        private void When(RootCreated e)
        {
            _rootId = e.RootId;
        }

        protected override void ConfigureStateMachine()
        {
            StateMachine = new StateMachine<State, Trigger>(State.Open);

            StateMachine.Configure(State.Open)
                .Permit(Trigger.Created, State.Complete);
            StateMachine.Configure(State.Complete)
                .OnEntry(() =>
                {
                    if (!_rootId.Contains("Copy"))
                        SendCommand(new CreateRoot {AggregateId =$"{_rootId}Copy"});

                });
            base.ConfigureStateMachine();
        }
    }

}