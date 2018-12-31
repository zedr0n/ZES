using Stateless;
using ZES.Infrastructure.Sagas;

namespace ZES.Tests.TestDomain
{
    public class TestSaga : StatelessSaga<TestSaga.State, TestSaga.Trigger>
    {
        public enum Trigger { Created } 
        public enum State { Open, Complete}

        private string _rootId = null;
        
        public TestSaga()
        {
            Register<RootCreated>(Trigger.Created, e => _rootId = e.RootId);
        }

        protected override void ConfigureStateMachine()
        {
            StateMachine = new StateMachine<State, Trigger>(State.Open);

            StateMachine.Configure(State.Open)
                .Permit(Trigger.Created, State.Complete);
            StateMachine.Configure(State.Complete)
                .OnEntry(() =>
                {
                    if (_rootId == "Root")
                        SendCommand(new CreateRootCommand {AggregateId = "RootNew"});

                });
            base.ConfigureStateMachine();
        }
    }

}