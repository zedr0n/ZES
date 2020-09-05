using System;
using System.Collections.Generic;
using System.Linq;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc cref="ISaga" />
    public abstract class Saga : EventSourced, ISaga
    {
        private readonly List<ICommand> _undispatchedCommands = new List<ICommand>();
        private readonly Dictionary<Type, Func<IEvent, string>> _sagaId = new Dictionary<Type, Func<IEvent, string>>();

        /// <inheritdoc />
        public IEnumerable<ICommand> GetUncommittedCommands()
        {
            lock (_undispatchedCommands)
                return _undispatchedCommands.ToArray();
        }

        /// <inheritdoc />
        public void SendCommand(ICommand command)
        {
            lock (_undispatchedCommands)
                _undispatchedCommands.Add(command);
        }

        /// <inheritdoc />
        public string SagaId(IEvent e)
        {
            var sagaId = _sagaId.SingleOrDefault(h => h.Key.IsInstanceOfType(e)).Value;
            return sagaId?.Invoke(e);
        }
        
        /// <inheritdoc />
        public override void LoadFrom<T>(IEnumerable<IEvent> pastEvents, bool computeHash = false)
        {
            base.LoadFrom<T>(pastEvents, computeHash);
            ClearUncommittedCommands();
        }

        /// <inheritdoc />
        public override void Clear()
        {
            ClearUncommittedCommands();
            base.Clear();
        }
        
        /// <inheritdoc />
        public override void Snapshot()
        {
            Version--;
            base.Snapshot();
            IgnoreCurrentEvent = true;
        }

        /// <summary>
        /// Apply the snapshot event to saga
        /// </summary>
        /// <param name="e">Snapshot event</param>
        protected abstract void ApplyEvent(ISagaSnapshotEvent e);

        /// <summary>
        /// Saga state hash
        /// </summary>
        protected virtual void DefaultHash() { }

        /// <summary>
        /// Associate the event with the specified saga id resolver
        /// and handle using the provided action
        /// <para> - Actions normally deal with saga state and can be null </para>
        /// </summary>
        /// <param name="sagaId">Id of saga handling this event</param>
        /// <param name="action">Handler applying the event to saga</param>
        /// <typeparam name="TEvent">Event type</typeparam>
        protected void Register<TEvent>(Func<TEvent, string> sagaId, Action<TEvent> action = null)
            where TEvent : class, IEvent
        {
            _sagaId[typeof(TEvent)] = e => sagaId(e as TEvent);
            var isSagaSnapshot = typeof(ISagaSnapshotEvent).IsAssignableFrom(typeof(TEvent));
            
            Action<TEvent> handler = e =>
            {
                action?.Invoke(e);
                DefaultHash();                               
            };
            if (action != null && isSagaSnapshot) 
            {
                handler = e =>
                {
                    ApplyEvent(e as ISagaSnapshotEvent);
                    action(e);
                };
            }

            Register(handler);
        }

        private void ClearUncommittedCommands()
        {
            lock (_undispatchedCommands) 
                _undispatchedCommands.Clear();
        }
    }   
}