using System;
using System.Collections.Generic;
using Stateless;
using ZES.Interfaces;

namespace ZES.Infrastructure.Sagas
{
    public abstract class StatelessSaga<TState, TTrigger> : Saga
    {
        private readonly Dictionary<Type, TTrigger> _triggers = new Dictionary<Type, TTrigger>();
        private StateMachine<TState, TTrigger> _stateMachine;
        
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

        public override void When(IEvent e)
        {
            base.When(e);
            if (_triggers.TryGetValue(e.GetType(), out var trigger) && trigger != null)
                StateMachine.Fire(trigger);
        }

        protected void Register<TEvent>(TTrigger t, Action<TEvent> action = null)
            where TEvent : class, IEvent
        {
            _triggers[typeof(TEvent)] = t;
            base.Register(action);
        }

        protected virtual void ConfigureStateMachine()
        {
        }
    }
}