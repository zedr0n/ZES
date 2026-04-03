using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Utils;
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
        private readonly Resolver _resolver;

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
            _resolver = new Resolver(bus);
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
            //var lastError = _log.Errors.Observable.FirstOrDefaultAsync().GetAwaiter().GetResult();
            /*_log.Errors.Observable.Subscribe(e =>
            {
                if (e != null && e != lastError)
                    isError = true;
            });*/
            
            //var task = _bus.CommandAsync(command).Result;
            _resolver.Post(command);
            var task = _resolver.OutputBlock.ReceiveAsync().Result;
            task.Wait();
            _manager.Ready.Wait();

            //var error = _log.Errors.Observable.FirstOrDefaultAsync(x => x?.OriginatingMessage?.MessageId == command.MessageId || x?.OriginatingMessage?.RetroactiveId == command.MessageId).Timeout(TimeSpan.FromMilliseconds(10),Observable.Return<IError>(null)).GetAwaiter().GetResult();
            var error = _log.Errors.PastErrors.LastOrDefault(x => x?.OriginatingMessage?.MessageId == command.MessageId || x?.OriginatingMessage?.RetroactiveId == command.MessageId);
            var isError = error != null;
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

        private class Resolver : Dataflow<ICommand, Task>
        {
            private readonly TransformBlock<ICommand, Task> _block;
            
            public Resolver(IBus bus) 
                : base(Configuration.DataflowOptions)
            {
                _block = new TransformBlock<ICommand, Task>(c => bus.CommandAsync(c), Configuration.DataflowOptions.ToExecutionBlockOption());
                RegisterChild(_block);
            }

            public override ITargetBlock<ICommand> InputBlock => _block;
            public override ISourceBlock<Task> OutputBlock => _block;
        }
    }
}