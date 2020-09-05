using System;
using Stateless;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Saga as a state machine, using <see cref="Stateless"/>
    /// </summary>
    /// <typeparam name="TState">State types</typeparam>
    /// <typeparam name="TTrigger">Trigger types</typeparam>
    public abstract class StatelessSaga<TState, TTrigger> : Saga
    {
        
        private StateMachine<TState, TTrigger> _stateMachine;

        /// <summary>
        /// Gets or sets gets state machine from <see cref="Stateless"/>
        /// </summary>
        /// <value>
        /// State machine from <see cref="Stateless"/>
        /// </value>
        protected StateMachine<TState, TTrigger> StateMachine
        {
            get
            {
                if (_stateMachine == null)
                    ConfigureStateMachine();
                return _stateMachine;
            }
            set => _stateMachine = value;
        }
        
        /// <summary>
        /// Gets or sets the initial state of the saga 
        /// </summary>
        protected TState InitialState { get; set; } = default(TState);

        /// <summary>
        /// Associate the event with the specified trigger, saga id resolver
        /// and handle using the provided action
        /// <para> - Actions normally deal with saga state and can be null </para>
        /// </summary>
        /// <param name="sagaId">Id of saga handling this event</param>
        /// <param name="t">Trigger value</param>
        /// <param name="action">Event handler</param>
        /// <typeparam name="TEvent">Handled event type</typeparam>
        protected void Register<TEvent>(Func<TEvent, string> sagaId, TTrigger t, Action<TEvent> action = null)
            where TEvent : class, IEvent
        {
            RegisterIf(sagaId, t, e => true, action);
        }
        
        /// <summary>
        /// Associate the event with the specified trigger, saga id resolver
        /// and handle using the provided action
        /// <para> - Actions normally deal with saga state and can be null </para>
        /// </summary>
        /// <param name="sagaId">Id of saga handling this event</param>
        /// <param name="t">Trigger value</param>
        /// <param name="predicate">Event predicate</param>
        /// <param name="action">Event handler</param>
        /// <typeparam name="TEvent">Handled event type</typeparam>
        protected void RegisterIf<TEvent>(Func<TEvent, string> sagaId, TTrigger t, Func<TEvent, bool> predicate, Action<TEvent> action = null)
            where TEvent : class, IEvent
        {
            void Handler(TEvent e)
            {
                var condition = predicate == null || predicate(e);
                if (!condition)
                {
                    IgnoreCurrentEvent = true;
                    return;
                }
                
                action?.Invoke(e);
                if (t == null) 
                    return;
                
                // we do not persist the events which are not registered
                if (StateMachine.CanFire(t))
                    StateMachine.Fire(t);
                else
                    IgnoreCurrentEvent = true;
            }
            
            Register(sagaId, Handler);
        }

        /// <inheritdoc />
        protected override void DefaultHash() => AddHash(StateMachine.State);

        /// <summary>
        /// State machine configuration 
        /// </summary>
        protected virtual void ConfigureStateMachine()
        {
            StateMachine = new StateMachine<TState, TTrigger>(InitialState);
        }

        /// <inheritdoc />
        protected override void ApplyEvent(ISnapshotEvent e)
        {
            if (!(e is SnapshotEvent snapshotEvent)) 
                return;
            
            StateMachine = null;    // clear the state machine to reinitialize the state
            InitialState = snapshotEvent.CurrentState;
            AddHash(StateMachine.State);
        }
        
        /// <summary>
        /// Snapshot event base class
        /// </summary>
        protected class SnapshotEvent : Event, ISnapshotEvent
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SnapshotEvent"/> class.
            /// </summary>
            /// <param name="state">Current state of the saga</param>
            public SnapshotEvent(TState state)
            {
                CurrentState = state;
            }

            public TState CurrentState { get; }
        }
    }
}