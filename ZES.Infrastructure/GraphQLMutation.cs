using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Base graphql mutation
    /// </summary>
    public class GraphQlMutation
    {
        private readonly IBus _bus;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphQlMutation"/> class.
        /// </summary>
        /// <param name="bus">Bus service</param>
        protected GraphQlMutation(IBus bus)
        {
            _bus = bus;
        }

        /// <summary>
        /// Execute command via bus 
        /// </summary>
        /// <param name="command">CQRS command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        /// <returns>True if command succeded</returns>
        protected bool CommandAsync<TCommand>(TCommand command)
            where TCommand : ICommand
        {
            var task = _bus.CommandAsync(command).Result;
            task.Wait();
            
            return true;
        }
    }
}