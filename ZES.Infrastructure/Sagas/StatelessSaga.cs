using System;
using System.Collections.Generic;
using Stateless;
using ZES.Interfaces;

namespace ZES.Infrastructure.Sagas
{
    /// <summary>
    /// Saga as a state machine, using <see cref="Stateless"/>
    /// </summary>
    /// <typeparam name="TState">State types</typeparam>
    /// <typeparam name="TTrigger">Trigger types</typeparam>
    public abstract class StatelessSaga<TState, TTrigger> : Saga
    {
        private readonly Dictionary<Type, TTrigger> _triggers = new Dictionary<Type, TTrigger>();
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

        /// <inheritdoc />
        public override void When(IEvent e)
        {
            base.When(e);
            if (_triggers.TryGetValue(e.GetType(), out var trigger) && trigger != null)
                StateMachine.Fire(trigger);
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
            _triggers[typeof(TEvent)] = t;
            base.Register(sagaId, action);
        }

        /// <summary>
        /// State machine configuration 
        /// </summary>
        protected virtual void ConfigureStateMachine()
        {
        }
    }
}