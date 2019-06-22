using System;
using Stateless;
using ZES.Infrastructure.Sagas;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Sagas
{
    public class TestSaga : StatelessSaga<TestSaga.State, TestSaga.Trigger>
    {
        private string _rootId;
        
        public TestSaga()
        {
            Register<RootCreated>(e => e.RootId, Trigger.Create, e => _rootId = e.RootId);
            Register<RootUpdated>(e => e.RootId, Trigger.Update);
        }
        
        public enum Trigger { Create, Update } 
        public enum State { Open, Complete }
        
        protected override void ConfigureStateMachine()
        {
            StateMachine = new StateMachine<State, Trigger>(State.Open);

            StateMachine.Configure(State.Open)
                .Permit(Trigger.Create, State.Complete);
            StateMachine.Configure(State.Complete)
                .Ignore(Trigger.Update)
                .OnEntry(() =>
                {
                    if (_rootId == string.Empty)
                        throw new InvalidOperationException();
                    if (!_rootId.Contains("Copy"))
                        SendCommand(new CreateRoot($"{_rootId}Copy"));
                });
            
            base.ConfigureStateMachine();
        }
    }
}