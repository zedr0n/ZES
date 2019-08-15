using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Causality
{
    /// <inheritdoc />
    public class NullGraph : IGraph
    {
        /// <inheritdoc />
        public void Initialize() { }

        /// <inheritdoc />
        public Task Pause(int ms) => Task.CompletedTask;

        /// <inheritdoc />
        public Task AddEvent(IEvent e) => Task.CompletedTask;

        /// <inheritdoc />
        public Task AddCommand(ICommand command) => Task.CompletedTask;
    }
}