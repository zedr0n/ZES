using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Gridsum.DataflowEx;
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
        private readonly GraphQlResolver _resolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphQlMutation"/> class.
        /// </summary>
        /// <param name="bus">Bus service</param>
        /// <param name="log">Log service</param>
        /// <param name="manager">Branch manager</param>
        /// <param name="resolver">GraphQL resolver</param>
        protected GraphQlMutation(IBus bus, ILog log, IBranchManager manager, GraphQlResolver resolver)
        {
            _bus = bus;
            _log = log;
            _manager = manager;
            _resolver = resolver;
        }

        /// <summary>
        /// Execute the query via bus
        /// </summary>
        /// <param name="query">Query instance</param>
        /// <typeparam name="TResult">Query result type</typeparam>
        /// <returns>Query result</returns>
        protected TResult Resolve<TResult>(IQuery<TResult> query)
        {
            var lastError = _log.Errors.Observable.FirstOrDefaultAsync().GetAwaiter().GetResult();

            var result = default(TResult);
            try
            {
                result = _bus.QueryAsync(query).Result;
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
            
            var error = _log.Errors.Observable.FirstOrDefaultAsync().GetAwaiter().GetResult();
            var isError = error != null && error != lastError;
            if (isError)
                throw new InvalidOperationException(error.Message);
            return result;
        }
        
        /// <summary>
        /// Execute command via bus 
        /// </summary>
        /// <param name="command">CQRS command</param>
        /// <returns>True if command succeeded</returns>
        protected bool Resolve(ICommand command)
        {
            var tracked = new Tracked<ICommand>(command);
            _resolver.Post(tracked);
            tracked.Task.Wait();
            
            //var error = _log.Errors.Observable.FirstOrDefaultAsync(x => x?.OriginatingMessage?.MessageId == command.MessageId || x?.OriginatingMessage?.RetroactiveId == command.MessageId).Timeout(TimeSpan.FromMilliseconds(10),Observable.Return<IError>(null)).GetAwaiter().GetResult();
            var error = _log.Errors.PastErrors.LastOrDefault(x => x?.OriginatingMessage?.MessageId == command.MessageId || x?.OriginatingMessage?.RetroactiveId == command.MessageId);
            var isError = error != null;
            return isError ? throw new InvalidOperationException(error.Message) : true;
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
            return isError ? throw new InvalidOperationException(error.Message) : true;
        }

    }
}