using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Domain;
using ZES.Interfaces.GraphQL;
using ZES.Interfaces.Infrastructure;

namespace ZES.Infrastructure.GraphQl
{
    /// <summary>
    /// Base graphql mutation
    /// </summary>
    public class GraphQlMutation : IGraphQlMutation
    {
        private readonly IBus _bus;
        private readonly ILog _log;
        private readonly IBranchManager _manager;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphQlMutation"/> class.
        /// </summary>
        /// <param name="bus">Bus service</param>
        /// <param name="log">Log service</param>
        /// <param name="manager">Branch manager</param>
        protected GraphQlMutation(IBus bus, ILog log, IBranchManager manager)
        {
            _bus = bus;
            _log = log;
            _manager = manager;
        }

        /// <summary>
        /// Execute command via bus 
        /// </summary>
        /// <param name="command">CQRS command</param>
        /// <typeparam name="TCommand">Command type</typeparam>
        /// <returns>True if command succeeded</returns>
        protected bool Resolve<TCommand>(TCommand command)
            where TCommand : ICommand
        {
            var lastError = _log.Errors.Observable.FirstOrDefaultAsync().GetAwaiter().GetResult();
            /*_log.Errors.Observable.Subscribe(e =>
            {
                if (e != null && e != lastError)
                    isError = true;
            });*/
            
            var task = _bus.CommandAsync(command).Result;
            task.Wait();
            _manager.Ready.Wait();

            var error = _log.Errors.Observable.FirstOrDefaultAsync().GetAwaiter().GetResult();
            var isError = error != null && error != lastError;
            if (isError && !error.Message.Contains("already exists in the command log"))
                throw new InvalidOperationException(error.Message);
            return !isError;
        }

        /// <summary>
        /// Processes a batch of commands and determines whether the operation was successful.
        /// </summary>
        /// <param name="commands">The list of commands to be executed as a batch.</param>
        /// <returns>
        /// A boolean value indicating whether the operation completed without critical errors.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a critical error occurs during command execution that is not related to duplicate commands in the log.
        /// </exception>
        protected bool Resolve(IEnumerable<ICommand> commands)
        {
            var lastError = _log.Errors.Observable.FirstOrDefaultAsync().GetAwaiter().GetResult();

            var task = _bus.CommandBatchAsync(commands).Result;
            task.Wait();
            _manager.Ready.Wait();
            
            var error = _log.Errors.Observable.FirstOrDefaultAsync().GetAwaiter().GetResult();
            var isError = error != null && error != lastError;
            if (isError && !error.Message.Contains("already exists in the command log"))
                throw new InvalidOperationException(error.Message);
            return !isError;
        }
    }
}