using System.Collections.Generic;
using ZES.Infrastructure.Stochastics;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Stochastic;
using ZES.Tests.Domain.Queries;

namespace ZES.Tests.Domain.Stochastics
{
    public class RecordPolicy : MarkovPolicy<BranchState>
    {
        private readonly IBranchManager _manager;
        private readonly IBus _bus;
        private readonly Dictionary<string, RecordAction> _actions = new Dictionary<string, RecordAction>();
        
        public RecordPolicy(IBus bus, IBranchManager manager)
        {
            _bus = bus;
            _manager = manager;
        }

        protected override MarkovPolicy<BranchState> Copy()
        {
            throw new System.NotImplementedException();
        }

        protected override IMarkovAction<BranchState>[] GetAllActions(BranchState state)
        {
            throw new System.NotImplementedException();
        }

        protected override IMarkovAction<BranchState> GetAction(BranchState state)
        {
            var total = _bus.QueryAsync(new TotalRecordQuery() { Timeline = state.Timeline }).Result.Total;
            if (total >= 10)
                return null;

            if (_actions.TryGetValue(state.Timeline, out var action)) 
                return action;
            
            action = new RecordAction(_manager, _bus);
            _actions[state.Timeline] = action;

            return action;
        }
    }
}