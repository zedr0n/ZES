using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Infrastructure.Projections;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Clocks;
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
        protected IProjection<TState> Projection { get; set; }

        /// <summary>
        /// Gets the projection manager 
        /// </summary>
        protected IProjectionManager Manager { get; }

        /// <summary>
        /// Gets or sets the projection predicate
        /// </summary>
        protected Func<IStream, bool> Predicate { get; set; } = s => true;
        
        /// <inheritdoc />
        protected override async Task<TResult> Handle(TQuery query) => await Handle(query, "");
        
        /// <summary>
        /// Handles a query on a specific projection and returns the result.
        /// </summary>
        /// <param name="query">The query to handle.</param>
        /// <param name="id">The identifier used to locate the appropriate projection, default is an empty string.</param>
        /// <returns>The result of the query.</returns>
        protected async Task<TResult> Handle(TQuery query, string id)
        {
            var projection = Projection;
            IEnumerable<IProjectionSink<TState>> sinks = [];
            var supportsHistoricalResults = typeof(IHistoricalState).IsAssignableFrom(typeof(TState)) &&
                                            typeof(IHistoricalResults<TResult>).IsAssignableFrom(typeof(TResult));
            
            if (query.Timeline != string.Empty)
            {
                projection = Manager.GetProjection<TState>(id, query.Timeline);
            }
            else if (query.Timestamp != null)
            {
                var historicalProjection = Manager.GetHistoricalProjection<TState>(id);
                historicalProjection.Timestamp = query.Timestamp;
                projection = historicalProjection;
            }
            else if (projection.Timeline != _activeTimeline.Id)
            {
                projection = Manager.GetProjection<TState>(id, _activeTimeline.Id);
            }
            else if (id != string.Empty)
            {
                projection = Manager.GetProjection<TState>(id);
            }
            projection.Predicate = Predicate;

            var sinkHost = projection as IProjectionSinkHost<TState>;
            if (query.AdditionalTimestamps is { Count: > 0 } && supportsHistoricalResults && sinkHost != null)
            {
                sinkHost.ClearSinks();
                sinks = query.AdditionalTimestamps.Select(x =>
                    new HistoricalProjectionSink<TState>(projection) { Timestamp = x }).ToList(); 
                sinkHost.AddSinks(sinks);

                // we need to restart the projection if additional timestamps are requested
                if(query.Timestamp == null)
                    await projection.Restart();
            }
            
            await projection.Ready;
            var result = await Handle(projection, query);
            if (supportsHistoricalResults && query.AdditionalTimestamps is { Count: > 0 })
            {
                var timestamp = query.Timestamp;
                try
                {
                    var historicalResults = (IHistoricalResults<TResult>)result;
                    if (historicalResults.HistoricalResults == null)
                        throw new InvalidOperationException("Historical results are not supported by this projection");

                    foreach (var sink in sinks)
                    {
                        query.Timestamp = sink.Latest;
                        historicalResults.HistoricalResults[sink.Latest] = await Handle(sink, query);
                    }
                }
                finally
                {
                    query.Timestamp = timestamp;
                    if(timestamp == null)
                        sinkHost?.ClearSinks();
                }
            }
            // historical projections are disposed of ( especially the subscriptions to streams ) immediately
            // this is fine because they are only accessed from the query handler
            if (query.Timestamp != null)
                projection.Dispose();
            return result;
        }
        
        /// <summary>
        /// Strongly-typed handler
        /// </summary>
        /// <param name="projection">Project</param>
        /// <param name="query">Query</param>
        /// <returns>Query result</returns>
        protected abstract Task<TResult> Handle(IProjectionState<TState> projection, TQuery query);
    }
}
