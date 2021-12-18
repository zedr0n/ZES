using System;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Aggregate root base class
    /// </summary>
    public class AggregateRoot : EventSourced, IAggregate
    {
        /// <inheritdoc />
        protected override void Register<TEvent>(Action<TEvent> action)
        {
            var handler = action;
            if (typeof(ISnapshotEvent).IsAssignableFrom(typeof(TEvent)))
            {
                handler = e =>
                {
                    action?.Invoke(e);
                    SnapshotVersion = e.Version;
                };
            }

            base.Register(handler);
        }
    }
}