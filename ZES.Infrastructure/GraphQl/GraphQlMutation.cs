using System;
using System.Reactive.Linq;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.GraphQL;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.GraphQl
{
    /// <summary>
    /// Base graphql mutation
    /// </summary>
    public class GraphQlMutation : IGraphQlMutation
    {
        private readonly IBus _bus;
        private readonly ILog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphQlMutation"/> class.
        /// </summary>
        /// <param name="bus">Bus service</param>
        /// <param name="log">Log service</param>
        protected GraphQlMutation(IBus bus, ILog log)
        {
            _bus = bus;
            _log = log;
        }

        /// <summary>
        /// Execute command via bus 
        /// </summary>
        /// <param name="command">CQRS command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        /// <returns>True if command succeded</returns>
        protected bool Resolve<TCommand>(TCommand command)
            where TCommand : ICommand
        {
            var isError = false;
            _log.Errors.Observable.Subscribe(e =>
            {
                if (e != null && e.ErrorType == nameof(InvalidOperationException))
                    isError = true;
            });
            
            var task = _bus.CommandAsync(command).Result;
            task.Wait();
            
            return !isError;
        }
    }
}