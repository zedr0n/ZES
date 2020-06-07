using System;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    public class SingleProjection<TState> : ProjectionBase<TState> 
        where TState : new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SingleProjection{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="log">Log service</param>
        /// <param name="timeline">Current timeline</param>
        public SingleProjection(IEventStore<IAggregate> eventStore, ILog log, ITimeline timeline) 
            : base(eventStore, log, timeline)
        {
        }

        public async Task Init(Func<IStream, bool> predicate)
        {
            Predicate = predicate;
            await Start();
        }
    }
}