using System.Threading.Tasks;
using ZES.Interfaces.Causality;

namespace ZES.Infrastructure.Causality
{
    /// <inheritdoc />
    public class NullGraph : IGraph
    {
        /// <inheritdoc />
        public Task Wait() => Task.CompletedTask;

        /// <inheritdoc />
        public Task Populate() => Task.CompletedTask;

        /// <inheritdoc />
        public Task Serialise(string filename = "streams.graphml") => Task.CompletedTask;

        /// <inheritdoc />
        public long GetTimestamp(string key, int version)
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc />
        public Task DeleteStream(string key) => Task.CompletedTask;

        /// <inheritdoc />
        public Task TrimStream(string key, int version) => Task.CompletedTask;
    }
}