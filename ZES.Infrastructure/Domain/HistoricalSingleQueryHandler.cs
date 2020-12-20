using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class HistoricalSingleQueryHandler<TQuery, TResult, TState> : HistoricalQueryHandler<TQuery, TResult, TState>
        where TQuery : class, ISingleQuery<TResult>
        where TResult : class, ISingleState, new()
        where TState : IState, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HistoricalSingleQueryHandler{TQuery, TResult, TState}"/> class.
        /// </summary>
        /// <param name="handler">Original query handler</param>
        /// <param name="manager">Projection manager</param>
        public HistoricalSingleQueryHandler(IQueryHandler<TQuery, TResult> handler, IProjectionManager manager)
            : base(handler, manager)
        {
        }

        /// <inheritdoc />
        protected override Task<TResult> Handle(HistoricalQuery<TQuery, TResult> query)
        {
            Projection = Manager.GetHistoricalProjection<TResult>(query.Query.Id);
            return base.Handle(query);
        }
    }
}