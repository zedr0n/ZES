using System.Collections.Generic;
using Gridsum.DataflowEx;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    internal class ProjectionDispatcher<TState> : ParallelDataDispatcher<string, Tracked<IStream>>
        where TState : new()
    {
        private readonly IProjectionRuntime<TState> _projection;
        private readonly IEnumerable<IProjectionSink<TState>> _additionalSinks;

        public ProjectionDispatcher(DataflowOptions options, IProjectionRuntime<TState> projection, IEnumerable<IProjectionSink<TState>> additionalSinks = null) 
            : base(s => s.Value.Key, options, projection.CancellationToken, projection.GetType())
        {
            _projection = projection;
            _additionalSinks = additionalSinks;
            Log = projection.Log;
                
            CompletionTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Log.Errors.Add(t.Exception);
            });
        }

        protected override Dataflow<Tracked<IStream>> CreateChildFlow(string dispatchKey) => new ProjectionFlow<TState>(m_dataflowOptions, _projection, _additionalSinks);
    }
}