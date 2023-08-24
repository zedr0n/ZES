using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class QueryHandlerBase<TQuery, TResult, TState> : QueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
        where TResult : class
        where TState : IState, new()
    {
        private readonly ITimeline _activeTimeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryHandlerBase{TQuery,TResult,TState}"/> class.
        /// </summary>
        /// <param name="manager">Projection manager</param>
        /// <param name="activeTimeline">Active timeline service</param>
        protected QueryHandlerBase(IProjectionManager manager, ITimeline activeTimeline)
        {
            _activeTimeline = activeTimeline;
            Projection = manager.GetProjection<TState>();
            Manager = manager;
        }

        /// <summary>
        /// Gets or sets gets the projection as common base interface
        /// </summary>
        /// <value>
        /// The projection as common base interface
        /// </value>
        protected IProjection Projection { get; set; }

        /// <summary>
        /// Gets the projection manager 
        /// </summary>
        protected IProjectionManager Manager { get; }

        /// <summary>
        /// Gets or sets the projection predicate
        /// </summary>
        protected Func<IStream, bool> Predicate { get; set; } = s => true;
        
        /// <inheritdoc />
        public override async Task<TResult> Handle(IProjection projection, TQuery query)
        {
            return await Handle(projection as IProjection<TState>, query);
        }

        /// <inheritdoc />
        protected override async Task<TResult> Handle(TQuery query)
        {
            var projection = Projection;
            if (query.Timeline != string.Empty)
            {
                projection = Manager.GetProjection<TState>(timeline: query.Timeline);
                projection.Predicate = Predicate;
            }
            else if (query.Timestamp != default)
            {
                var historicalProjection = Manager.GetHistoricalProjection<TState>();
                historicalProjection.Timestamp = query.Timestamp;
                historicalProjection.Predicate = Predicate;
                projection = historicalProjection;
            }
            else if (projection.Timeline != _activeTimeline.Id)
                projection = Manager.GetProjection<TState>(timeline: _activeTimeline.Id);

            await projection.Ready;
            return await Handle(projection, query);
        }

        /// <summary>
        /// Strongly-typed handler
        /// </summary>
        /// <param name="projection">Project</param>
        /// <param name="query">Query</param>
        /// <returns>Query result</returns>
        protected abstract Task<TResult> Handle(IProjection<TState> projection, TQuery query);
    }
}