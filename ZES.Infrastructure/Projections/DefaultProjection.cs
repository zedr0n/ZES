using System.Collections.Generic;
using System.Linq;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    /// <inheritdoc />
    public class DefaultProjection<TState> : GlobalProjection<TState> 
        where TState : IState, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultProjection{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="log">Log service</param>
        /// <param name="activeTimeline">Branch</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="streamLocator">Stream locator</param>
        /// <param name="handlers">Event handlers</param>
        public DefaultProjection(IEventStore<IAggregate> eventStore, ILog log, ITimeline activeTimeline, IMessageQueue messageQueue, IStreamLocator streamLocator, IEnumerable<IProjectionHandler<TState>> handlers)
            : base(eventStore, log, activeTimeline, messageQueue, streamLocator)
        {
            State = new TState();
            foreach (var h in handlers)
            {
                var typeArguments =
                    h.GetType().GetInterfaces().Where(i => i.GenericTypeArguments.Length > 1).Select(i => i.GenericTypeArguments[1]);
                foreach (var tEvent in typeArguments)
                    Register(tEvent, h.Handle);
            }
        }
    }
}