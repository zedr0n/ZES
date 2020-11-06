using System;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Stochastics
{
    /// <inheritdoc />
    public abstract class BranchReward<TResult> : ActionReward<BranchState, BranchAction>
    {
        private readonly IBus _bus;

        /// <summary>
        /// Initializes a new instance of the <see cref="BranchReward{TResult}"/> class.
        /// </summary>
        /// <param name="bus">Query bus</param>
        protected BranchReward(IBus bus)
        {
            _bus = bus;
        }

        /// <summary>
        /// Gets the branch value query
        /// </summary>
        protected abstract IQuery<TResult> ValueQuery { get; }
        
        /// <summary>
        /// Gets the branch value query result -> double resolver
        /// </summary>
        protected abstract Func<TResult, double> Value { get; }

        /// <inheritdoc/>
        public override double this[BranchState from, BranchState to, BranchAction action]
        {
            get
            {
                var query = ValueQuery;
                query.Timeline = from.Timeline;
                var fromResult = _bus.QueryAsync(query).Result;
                query.Timeline = to.Timeline;
                var toResult = _bus.QueryAsync(query).Result;
                return Value(toResult) - Value(fromResult);
            }
        } 
    }
}