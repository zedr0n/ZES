using System;
using System.Collections.Generic;
using System.Linq;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc cref="ISaga" />
    public abstract class Saga : EventSourced, ISaga
    {
        private readonly List<ICommand> _undispatchedCommands = new List<ICommand>();
        private readonly Dictionary<Type, Func<IEvent, string>> _sagaId = new Dictionary<Type, Func<IEvent, string>>();
        private readonly Dictionary<Type, Func<IEvent, IEvent>> _events = new Dictionary<Type, Func<IEvent, IEvent>>();

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
            if (e == null)
                return null;
            var sagaId = _sagaId.SingleOrDefault(h => h.Key.IsInstanceOfType(e)).Value;
            return sagaId?.Invoke(e);
        }

        /// <inheritdoc />
        public IEvent ToSagaEvent(IEvent e)
        {
            if (!_events.TryGetValue(e.GetType(), out var converter)) 
                return e.Copy();
            
            var sagaEvent = converter(e);
            sagaEvent.AncestorId = e.AncestorId ?? e.MessageId;
            
            sagaEvent.StaticMetadata.OriginatingStream = e.Stream;
            sagaEvent.CommandId = e.CommandId;
            sagaEvent.Timestamp = e.Timestamp;
            return sagaEvent;
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
        /// Register the aggregate snapshot trigger
        /// </summary>
        /// <typeparam name="TRoot">Aggregate type</typeparam>
        protected void RegisterOnSnapshot<TRoot>()
        {
            Register<ISnapshotEvent<TRoot>>(e => e.AggregateRootId(), _ =>
            {
                // version needs to be decremented as the aggregate event is ignored
                Version--;
                Snapshot();
                IgnoreCurrentEvent = true;
            });
        }

        /// <summary>
        /// Register event converter
        /// </summary>
        /// <param name="converter">Saga event converter</param>
        /// <typeparam name="TEvent">Event type</typeparam>
        protected void RegisterEvent<TEvent>(Func<TEvent, IEvent> converter)
            where TEvent : class, IEvent
        {
            _events[typeof(TEvent)] = e => converter(e as TEvent);
        }
        
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

            void Handler(TEvent e)
            {
                if (isSagaSnapshot) 
                    ApplyEvent(e as ISagaSnapshotEvent);
                action?.Invoke(e);
                DefaultHash();
            }

            Register((Action<TEvent>)Handler);
        }

        private void ClearUncommittedCommands()
        {
            lock (_undispatchedCommands) 
                _undispatchedCommands.Clear();
        }
    }   
}