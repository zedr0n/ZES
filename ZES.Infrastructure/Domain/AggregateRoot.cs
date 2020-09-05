using System;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Aggregate root base class
    /// </summary>
    public class AggregateRoot : EventSourced
    {
        /// <inheritdoc />
        protected override void Register<TEvent>(Action<TEvent> action)
        {
            var handler = action;
            if (action != null && typeof(ISnapshotEvent).IsAssignableFrom(typeof(TEvent)))
            {
                handler = e =>
                {
                    action(e);
                    SnapshotVersion = e.Version;
                };
            }

            base.Register(handler);
        }
    }
}