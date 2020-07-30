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
            void Handler(TEvent e)
            {
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

        /// <summary>
        /// State machine configuration 
        /// </summary>
        protected abstract void ConfigureStateMachine();
    }
}