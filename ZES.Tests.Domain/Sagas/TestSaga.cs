using System;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Sagas
{
    public class TestSaga : StatelessSaga<TestSaga.State, TestSaga.Trigger>
    {
        private string _rootId;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestSaga"/> class.
        /// </summary>
        public TestSaga()
        {
            Register<RootCreated>(e => e.RootId, Trigger.Create, e => _rootId = e.RootId);
            Register<RootUpdated>(e => e.RootId, Trigger.Update);
            Register<ISnapshotEvent<Root>>(e => e.Id, _ =>
            {
                Snapshot();
                IgnoreCurrentEvent = true;
            });
            Register<TestSagaSnapshotEvent>(e => e.Id, e => _rootId = e.Id);
        }
        
        public enum Trigger 
        { 
            /// <summary>
            /// Root created
            /// </summary>
            Create,
            
            /// <summary>
            /// Root updated
            /// </summary>
            Update, 
        }

        public enum State
        {
            /// <summary>
            /// Saga started
            /// </summary>
            Open,
            
            /// <summary>
            /// Saga completed
            /// </summary>
            Complete,
        }

        /// <inheritdoc/>
        protected override ISnapshotEvent CreateSnapshot() => new TestSagaSnapshotEvent(StateMachine.State, _rootId);

        /// <inheritdoc/>
        protected override void ConfigureStateMachine()
        {
            base.ConfigureStateMachine(); 

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
        }

        private class TestSagaSnapshotEvent : SnapshotEvent
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TestSagaSnapshotEvent"/> class.
            /// </summary>
            /// <param name="state">Current state</param>
            /// <param name="rootId">Root id</param>
            public TestSagaSnapshotEvent(State state, string rootId) 
                : base(rootId, state)
            {
            }
        }
    }
}