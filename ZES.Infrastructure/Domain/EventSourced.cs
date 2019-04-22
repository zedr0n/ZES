using System;
using System.Collections.Generic;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    public abstract class EventSourced : IEventSourced
    {
        private readonly List<IEvent> _changes = new List<IEvent>();
        private readonly Dictionary<Type, Action<IEvent>> _handlers = new Dictionary<Type, Action<IEvent>>();

        public string Id { get; protected set; }
        public int Version { get; private set; } = -1;

        public static T Create<T>(string id)
            where T : class, IEventSourced, new()
        {
            var instance = new T() as EventSourced;
            if (instance == null)
                return default(T);

            instance.Id = id;
            return instance as T;
        }

        public IEnumerable<IEvent> GetUncommittedEvents()
        {
            lock (_changes)
                return _changes.ToArray();
        }

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

        public virtual void LoadFrom<T>(IEnumerable<IEvent> pastEvents)
            where T : class, IEventSourced
        {
            foreach (var e in pastEvents)
                When(e);

            ClearUncommittedEvents();
        }

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