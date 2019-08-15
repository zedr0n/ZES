using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Interfaces.Causality;

namespace ZES.Infrastructure.Causality
{
    /// <inheritdoc />
    public class NullReadGraph : IReadGraph
    {
        /// <inheritdoc />
        public IObservable<GraphReadState> State => Observable.Return(GraphReadState.Sleeping);

        /// <inheritdoc />
        public void Start() { }

        /// <inheritdoc />
        public Task Pause() => Task.CompletedTask;

        /// <inheritdoc />
        public Task<int> GetStreamVersion(string key) => Task.FromResult(0);

        /// <inheritdoc />
        public void Export(string path) { }
    }
}