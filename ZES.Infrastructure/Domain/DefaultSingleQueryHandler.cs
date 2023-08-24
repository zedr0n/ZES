using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class DefaultSingleQueryHandler<TQuery, TResult, TState> : DefaultQueryHandler<TQuery, TResult, TState>
        where TQuery : class, ISingleQuery<TResult> 
        where TResult : class
        where TState : class, ISingleState, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultSingleQueryHandler{TQuery, TResult, TState}"/> class.
        /// </summary>
        /// <param name="manager">Projection manager</param>
        /// <param name="activeTimeline">Active timeline</param>
        public DefaultSingleQueryHandler(IProjectionManager manager, ITimeline activeTimeline)
            : base(manager, activeTimeline)
        {
        }

        /// <inheritdoc />
        protected override async Task<TResult> Handle(TQuery query)
        {
            Projection = Manager.GetProjection<TState>(query.Id);
            Predicate = Projection.Predicate;
            return await base.Handle(query);
        }
    }
    
    /// <inheritdoc />
    public class DefaultSingleQueryHandler<TQuery, TResult> : DefaultSingleQueryHandler<TQuery, TResult, TResult> 
        where TQuery : class, ISingleQuery<TResult> 
        where TResult : class, ISingleState, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultSingleQueryHandler{TQuery,TResult}"/> class.
        /// </summary>
        /// <param name="manager">Projection manager</param>
        /// <param name="activeTimeline">Active timeline</param>
        public DefaultSingleQueryHandler(IProjectionManager manager, ITimeline activeTimeline)
            : base(manager, activeTimeline)
        {
        }
    }
}