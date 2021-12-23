using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Events;

namespace ZES.Tests.Domain.Sagas
{
    /// <inheritdoc />
    public class TestSaga : StatelessSaga<TestSaga.State, TestSaga.Trigger>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestSaga"/> class.
        /// </summary>
        public TestSaga()
        {
            RegisterWithParameters<RootCreated>(e => e.RootId, Trigger.Create);
            RegisterWithParameters<RootUpdated>(e => e.AggregateRootId(), Trigger.Update);
            RegisterOnSnapshot<Root>();
            Register<TestSagaSnapshotEvent>(e => e.Id);
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
        protected override ISnapshotEvent CreateSnapshot() => new TestSagaSnapshotEvent(StateMachine.State, Id);

        /// <inheritdoc/>
        protected override void ConfigureStateMachine()
        {
            base.ConfigureStateMachine();
            
            var eventTrigger = GetTrigger<RootCreated>();
            StateMachine.Configure(State.Open)
                .Permit(Trigger.Create, State.Complete);
            StateMachine.Configure(State.Complete)
                .Ignore(Trigger.Update)
                .OnEntryFrom(eventTrigger, Handle);
        }

        private void Handle(RootCreated e)
        {
            if (!e.RootId.Contains("Copy"))
                SendCommand(new CreateRoot($"{e.RootId}Copy"));
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