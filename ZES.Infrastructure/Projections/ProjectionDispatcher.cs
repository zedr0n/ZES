using Gridsum.DataflowEx;
using ZES.Infrastructure.Dataflow;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    internal class ProjectionDispatcher<TState> : ParallelDataDispatcher<string, Tracked<IStream>>
        where TState : new()
    {
        private readonly ProjectionBase<TState> _projection;

        public ProjectionDispatcher(DataflowOptions options, ProjectionBase<TState> projection) 
            : base(s => s.Value.Key, options, projection.CancellationToken, projection.GetType())
        {
            _projection = projection;
            Log = projection.Log;
                
            CompletionTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Log.Errors.Add(t.Exception);
            });
        }

        protected override Dataflow<Tracked<IStream>> CreateChildFlow(string dispatchKey) => new ProjectionFlow<TState>(m_dataflowOptions, _projection);
    }
}