using System;
using System.Collections.Generic;
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
        where TState : Enum
    {
        private readonly List<Action<StateMachine<TState, TTrigger>>> _configurationActions = new List<Action<StateMachine<TState, TTrigger>>>();
        private StateMachine<TState, TTrigger> _stateMachine;
        
        /// <summary>
        /// Gets the current state
        /// </summary>
        public TState CurrentState => StateMachine.State;

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
        /// Gets the triggers combined with the associated event
        /// </summary>
        private Dictionary<Type, StateMachine<TState, TTrigger>.TriggerWithParameters> Triggers { get; } =
            new Dictionary<Type, StateMachine<TState, TTrigger>.TriggerWithParameters>();

        /// <summary>
        /// Associate the event with the specified trigger using correlation id for saga id resolver
        /// and handle using the provided action
        /// <para> - Actions normally deal with saga state and can be null </para>
        /// </summary>
        /// <param name="t">Trigger value</param>
        /// <param name="action">Event handler</param>
        /// <typeparam name="TEvent">Handled event type</typeparam>
        protected void Register<TEvent>(TTrigger t, Action<TEvent> action = null)
            where TEvent : class, IEvent
        {
            RegisterIf(e => e.CorrelationId, e => t, e => true, action);
        }

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
            RegisterIf(sagaId, e => t, e => true, action);
        }
      
        /// <summary>
        /// Associate the event with the specified trigger using correlation id for saga id resolver
        /// <para> - Actions normally deal with saga state and can be null </para>
        /// </summary>
        /// <param name="t">Trigger value</param>
        /// <typeparam name="TEvent">Handled event type</typeparam>
        protected void RegisterWithParameters<TEvent>(TTrigger t)
            where TEvent : class, IEvent
        {
            RegisterIfWithParameters<TEvent>(e => e.CorrelationId, t, e => true);
        }

        /// <summary>
        /// Associate the event with the specified trigger, saga id resolver
        /// <para> - Actions normally deal with saga state and can be null </para>
        /// </summary>
        /// <param name="sagaId">Id of saga handling this event</param>
        /// <param name="t">Trigger value</param>
        /// <typeparam name="TEvent">Handled event type</typeparam>
        protected void RegisterWithParameters<TEvent>(Func<TEvent, string> sagaId, TTrigger t)
            where TEvent : class, IEvent
        {
            RegisterIfWithParameters(sagaId, t, e => true);
        }

        /// <summary>
        /// Associate the event with the specified trigger, saga id resolver
        /// and handle using the provided action
        /// <para> - Actions normally deal with saga state and can be null </para>
        /// </summary>
        /// <param name="sagaId">Id of saga handling this event</param>
        /// <param name="t">Trigger value</param>
        /// <param name="action">Event handler</param>
        /// <typeparam name="TEvent">Handled event type</typeparam>
        protected void Register<TEvent>(Func<TEvent, string> sagaId, Func<TEvent, TTrigger> t, Action<TEvent> action = null)
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
        /// <param name="triggerFunc">Trigger value</param>
        /// <param name="predicate">Event predicate</param>
        /// <param name="action">Event handler</param>
        /// <typeparam name="TEvent">Handled event type</typeparam>
        protected void RegisterIf<TEvent>(Func<TEvent, string> sagaId, Func<TEvent, TTrigger> triggerFunc, Func<TEvent, bool> predicate, Action<TEvent> action = null)
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
                var t = triggerFunc(e);
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

        /// <summary>
        /// Associate the event with the specified trigger, saga id resolver
        /// and handle using the provided action
        /// <para> - Actions normally deal with saga state and can be null </para>
        /// </summary>
        /// <param name="sagaId">Id of saga handling this event</param>
        /// <param name="trigger">Base trigger</param>
        /// <param name="predicate">Event predicate</param>
        /// <typeparam name="TEvent">Handled event type</typeparam>
        protected void RegisterIfWithParameters<TEvent>(
            Func<TEvent, string> sagaId,
            TTrigger trigger,
            Func<TEvent, bool> predicate)
            where TEvent : class, IEvent
        {
            void Handler(TEvent e)
            {
                var condition = (predicate == null || predicate(e)) && StateMachine.CanFire(trigger);
                if (!condition)
                {
                    IgnoreCurrentEvent = true;
                    return;
                }

                var triggerWithParameters =
                    Triggers[typeof(TEvent)] as StateMachine<TState, TTrigger>.TriggerWithParameters<TEvent>;
                StateMachine.Fire(triggerWithParameters, e);
            }

            void ConfigurationAction(StateMachine<TState, TTrigger> stateMachine)
            {
                if (Triggers.ContainsKey(typeof(TEvent)))
                    throw new Exception($"{typeof(TEvent).Name} already registered with saga {GetType().Name}");
            
                Triggers[typeof(TEvent)] = stateMachine.SetTriggerParameters<TEvent>(trigger);
            }
            
            _configurationActions.Add(ConfigurationAction);
            
            Register(sagaId, Handler);
        }

        /// <summary>
        /// Get the associated trigger with parameters for the event
        /// </summary>
        /// <typeparam name="TEvent">Event type</typeparam>
        /// <returns>Trigger with parameters</returns>
        protected StateMachine<TState, TTrigger>.TriggerWithParameters<TEvent> GetTrigger<TEvent>()
            where TEvent : class, IEvent
        {
            if (Triggers.TryGetValue(typeof(TEvent), out var t))
                return t as StateMachine<TState, TTrigger>.TriggerWithParameters<TEvent>;
            return null;
        }

        /// <inheritdoc />
        protected override void DefaultHash() => AddHash(StateMachine.State);

        /// <summary>
        /// State machine configuration 
        /// </summary>
        protected virtual void ConfigureStateMachine()
        {
            StateMachine = new StateMachine<TState, TTrigger>(InitialState);
            foreach (var configurationAction in _configurationActions)
                configurationAction(StateMachine);
        }

        /// <inheritdoc />
        protected override void ApplyEvent(ISagaSnapshotEvent e)
        {
            if (!(e is SnapshotEvent snapshotEvent)) 
                return;
            
            StateMachine = null;    // clear the state machine to reinitialize the state
            InitialState = (TState)Enum.Parse(typeof(TState), snapshotEvent.CurrentState);
            SnapshotVersion = snapshotEvent.Version;
            Triggers.Clear();
        }
        
        /// <summary>
        /// Snapshot event base class
        /// </summary>
        protected class SnapshotEvent : Event, ISagaSnapshotEvent
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SnapshotEvent"/> class.
            /// </summary>
            /// <param name="id">Event-sourced id</param>
            /// <param name="state">Current state of the saga</param>
            public SnapshotEvent(string id, TState state)
            {
                Id = id;
                CurrentState = state.ToString();
            }

            /// <summary>
            /// Gets or sets the saga current state
            /// </summary>
            public string CurrentState { get; set; }

            /// <inheritdoc />
            public string Id { get; set; }
        }
    }
}