using System;
using System.Collections.Generic;
using System.Linq;
using SqlStreamStore.Streams;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class EventSourced : IEventSourced
    {
        private readonly List<IEvent> _changes = new List<IEvent>();
        private readonly List<IEvent> _invalidEvents = new List<IEvent>();
        private readonly Dictionary<Type, Action<IEvent>> _handlers = new Dictionary<Type, Action<IEvent>>();

        private string _hash;
        private bool _computeHash;

        /// <inheritdoc />
        public bool IsValid => _invalidEvents.Count == 0;

        /// <inheritdoc />
        public int LastValidVersion => _invalidEvents.Min(e => e.Version) - 1; 
        
        /// <inheritdoc />
        public string Id { get; protected set; }

        /// <inheritdoc />
        public int Version { get; private set; } = -1;

        /// <inheritdoc />
        public long Timestamp { get; private set; }

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

        /// <inheritdoc />
        public IEnumerable<IEvent> GetInvalidEvents()
        {
            lock (_invalidEvents)
                return _invalidEvents.ToArray();
        }

        /// <inheritdoc />
        public void TimestampEvents(long timestamp)
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
        public virtual void When(IEvent e)
        {
            lock (_changes)
            {
                ApplyEvent(e, true);
                e.Hash = _hash;
                
                ((Event)e).Version = Version;
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
                ApplyEvent(e, computeHash);

            Timestamp = enumerable.Max(e => e.Timestamp);

            ClearUncommittedEvents();
        }
        
        /// <summary>
        /// Update the hash with object
        /// </summary>
        /// <param name="value">Object to hash</param>
        protected void Hash(object value)
        {
            if (!_computeHash)
                return;
            
            var objectHash = Hashing.Sha256(value);
            _hash = Hashing.Sha256(_hash + objectHash);
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

        /// <summary>
        /// Apply the event to the instance
        /// </summary>
        /// <param name="e">Event</param>
        /// <param name="computeHash">True to compute the event hashes</param>
        private void ApplyEvent(IEvent e, bool computeHash = false)
        {
            if (e == null)
                return;
            
            Version++;

            if (computeHash)
            {
                _computeHash = true;
                _hash = string.Empty;
            }
            
            if (_handlers.TryGetValue(e.GetType(), out var handler))
                handler(e);

            if (!computeHash)
                return;
            
            if (e.Hash != _hash)
                _invalidEvents.Add(e);
        }

        private void ClearUncommittedEvents()
        {
            lock (_changes)
                _changes.Clear();
        }
    }
}