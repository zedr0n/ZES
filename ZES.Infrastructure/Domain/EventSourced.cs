using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;
using ZES.Interfaces;
using ZES.Interfaces.Clocks;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class EventSourced : IEventSourced
    {
        private readonly List<IEvent> _changes = new();
        private readonly List<IEvent> _invalidEvents = new();
        private readonly Dictionary<Type, Action<IEvent>> _handlers = new();
        private readonly List<object> _state = new();

        /// <summary>
        /// Gets or sets the log service
        /// </summary>
        public ILog Log { get; set; }
        
        /// <inheritdoc />
        public bool IsValid => _invalidEvents.Count == 0;

        /// <inheritdoc />
        public int LastValidVersion => _invalidEvents.Min(e => e.Version) - 1; 
        
        /// <inheritdoc />
        public string Id { get; protected set; }

        /// <inheritdoc />
        public int Version { get; protected set; } = -1;

        /// <inheritdoc />
        public Time Timestamp { get; private set; }

        /// <summary>
        /// Gets or sets the last snapshot version
        /// </summary>
        public int SnapshotVersion { get; protected set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the current event should be ignored
        /// </summary>
        protected bool IgnoreCurrentEvent { get; set; }

        /// <summary>
        /// Static event sourced instance factory
        /// </summary>
        /// <param name="id">Event sourced identifier</param>
        /// <param name="version">Initial version</param>
        /// <param name="log">Log service</param>
        /// <typeparam name="T">Event sourced type</typeparam>
        /// <returns>Event sourced instance with the provided id</returns>
        public static T Create<T>(string id, int version, ILog log = null)
            where T : class, IEventSourced, new()
        {
            var instance = new T() as EventSourced;
            if (instance == null)
                return default(T);

            instance.Id = id;
            instance.Log = log;
            if (version > 0)
                instance.Version = version;
            return instance as T;
        }
        
        /// <inheritdoc />
        public IEnumerable<IEvent> GetUncommittedEvents()
        {
            lock (_changes)
                return _changes.ToArray();
        }

        /// <inheritdoc />
        public IEnumerable<IEvent> GetInvalidEvents()
        {
            lock (_invalidEvents)
                return _invalidEvents.ToArray();
        }

        /// <inheritdoc />
        public virtual void Clear()
        {
            ClearUncommittedEvents();
        }

        /// <inheritdoc />
        public void TimestampEvents(Time timestamp)
        {
            lock (_changes)
            {
                foreach (var c in _changes)
                    c.Timestamp = timestamp;
            }
        }
        
        /// <summary>
        /// Base event handler
        /// <para>* Applies the event to the event sourced instance 
        /// <para>* Updates the version of the event
        /// </para></para>
        /// </summary>
        /// <param name="e">Generated event</param>
        public void When(IEvent e)
        {
            lock (_changes)
            {
                ApplyEvent(e);
                e.ContentHash = Hashing.Crc32(_state);
                
                e.Version = Version;
                if (!IgnoreCurrentEvent)
                    _changes.Add(e);
            }
        }

        /// <inheritdoc />
        public virtual void LoadFrom<T>(IEnumerable<IEvent> pastEvents, bool computeHash = false)
            where T : class, IEventSourced
        {
            _invalidEvents.Clear();
            
            var enumerable = pastEvents.ToList();
            foreach (var e in enumerable)
            {
                ApplyEvent(e);
                if (e.ContentHash != Hashing.Crc32(_state))
                    _invalidEvents.Add(e);
            }

            Timestamp = enumerable.Max(e => e.Timestamp);

            ClearUncommittedEvents();
        }

        /// <inheritdoc />
        public virtual void Snapshot()
        {
            var e = CreateSnapshot();
            if (e == null) 
                return;

            e.Version = Version + 1;
            When(e);
        }

        /// <summary>
        /// Snapshot creation virtual method
        /// </summary>
        /// <returns>Snapshot of current state, if snapshotting is available</returns>
        protected virtual ISnapshotEvent CreateSnapshot() => null;
        
        /// <summary>
        /// Update the hash with object
        /// </summary>
        /// <param name="value">Object to hash</param>
        protected void AddHash(object value)
        {
            _state.Add(value);
        }
        
        /// <summary>
        /// Register the action to apply event to the instance
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <typeparam name="TEvent">Event type</typeparam>
        protected virtual void Register<TEvent>(Action<TEvent> handler)
            where TEvent : class, IEvent
        {
            if (handler != null)
                _handlers.Add(typeof(TEvent), e => handler(e as TEvent));
        }
        
        /// <summary>
        /// Apply the event to the instance
        /// </summary>
        /// <param name="e">Event</param>
        private void ApplyEvent(IEvent e)
        {
            _state.Clear();
            if (e == null)
                return;

            IgnoreCurrentEvent = false;
            Version++;
            
            // if (_handlers.TryGetValue(e.GetType(), out var handler))
            var handler = _handlers.SingleOrDefault(h => h.Key.IsInstanceOfType(e)).Value;
            handler?.Invoke(e);
        }

        private void ClearUncommittedEvents()
        {
            lock (_changes)
                _changes.Clear();
        }
    }
}