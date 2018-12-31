using System;
using System.Collections.Generic;
using Stateless;
using ZES.Interfaces;

namespace ZES.Infrastructure.Sagas
{
    public abstract class StatelessSaga<TState,TTrigger> : Saga
    {
        protected StateMachine<TState, TTrigger> StateMachine
        {
            get
            {
                if(_stateMachine == null)
                    ConfigureStateMachine();
                return _stateMachine;
            }
            set => _stateMachine = value;
        }
        private StateMachine<TState, TTrigger> _stateMachine;
        private readonly Dictionary<Type, TTrigger> _triggers = new Dictionary<Type, TTrigger>();

        protected override void When(IEvent e)
        {
            if (_triggers.TryGetValue(e.GetType(), out var trigger))
                StateMachine.Fire(trigger);
            base.When(e);
        }

        protected void Register<TEvent>(TTrigger t)
            where TEvent : class, IEvent
        {
            _triggers[typeof(TEvent)] = t;
        }

        protected virtual void ConfigureStateMachine()
        {
        }
    }
}