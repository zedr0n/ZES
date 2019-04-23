using System;
using System.Collections.Generic;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class EventSourced : IEventSourced
    {
        private readonly List<IEvent> _changes = new List<IEvent>();
        private readonly Dictionary<Type, Action<IEvent>> _handlers = new Dictionary<Type, Action<IEvent>>();

        /// <inheritdoc />
        public string Id { get; protected set; }

        /// <inheritdoc />
        public int Version { get; private set; } = -1;

        /// <summary>
        /// Static event sourced instance factory
        /// </summary>
        /// <param name="id">Event sourced identifier</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <returns>Event sourced instance with the provided id</returns>
        public static T Create<T>(string id)
            where T : class, IEventSourced, new()
        {
            var instance = new T() as EventSourced;
            if (instance == null)
                return default(T);

            instance.Id = id;
            return instance as T;
        }

        /// <inheritdoc />
        public IEnumerable<IEvent> GetUncommittedEvents()
        {
            lock (_changes)
                return _changes.ToArray();
        }

        /// <summary>
        /// Base event handler
        /// <para>* Applies the event to the event sourced instance 
        /// <para>* Update the version and persist to the event
        /// </para></para>
        /// </summary>
        /// <param name="e">Generated event</param>
        public virtual void When(IEvent e)
        {
            lock (_changes)
            {
                ApplyEvent(e);
                Version++;
                ((Event)e).Version = Version;
                _changes.Add(e);
            }
        }

        /// <inheritdoc />
        public virtual void LoadFrom<T>(IEnumerable<IEvent> pastEvents)
            where T : class, IEventSourced
        {
            foreach (var e in pastEvents)
                When(e);

            ClearUncommittedEvents();
        }

        /// <summary>
        /// Register the action to apply event to the instance
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <typeparam name="TEvent">Event type</typeparam>
        protected void Register<TEvent>(Action<TEvent> handler)
            where TEvent : class, IEvent
        {
            if (handler != null)
                _handlers.Add(typeof(TEvent), e => handler(e as TEvent));
        }

        private void ClearUncommittedEvents()
        {
            lock (_changes)
                _changes.Clear();
        }

        private void ApplyEvent(IEvent e)
        {
            if (e == null)
                return;

            if (_handlers.TryGetValue(e.GetType(), out var handler))
                handler(e);
        }
    }
}