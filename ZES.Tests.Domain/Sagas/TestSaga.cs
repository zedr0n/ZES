using Stateless;
using ZES.Infrastructure.Sagas;
using ZES.Interfaces;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Sagas
{
    public class TestSaga : StatelessSaga<TestSaga.State, TestSaga.Trigger>
    {
        private string _rootId;
        
        public TestSaga()
        {
            Register<RootCreated>(e => e.RootId, Trigger.Created, When);
            Register<RootUpdated>(e => e.RootId, Trigger.Created);
        }
        
        public enum Trigger { Created } 
        public enum State { Open, Complete }
        
        protected override void ConfigureStateMachine()
        {
            StateMachine = new StateMachine<State, Trigger>(State.Open);

            StateMachine.Configure(State.Open)
                .Permit(Trigger.Created, State.Complete);
            StateMachine.Configure(State.Complete)
                .OnEntry(() =>
                {
                    if (!_rootId.Contains("Copy"))
                        SendCommand(new CreateRoot($"{_rootId}Copy"));
                });
            
            base.ConfigureStateMachine();
        }

        private void When(RootCreated e)
        {
            _rootId = e.RootId;
        }
    }
}