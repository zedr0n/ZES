using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SimpleInjector;
using ZES.Infrastructure;
using ZES.Infrastructure.Dataflow;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES
{
    /// <inheritdoc />
    public class Bus : IBus
    {
        private readonly Container _container;
        private readonly CommandDispatcher _commandDispatcher;
        private readonly ILog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bus"/> class.
        /// </summary>
        /// <param name="container"><see cref="SimpleInjector"/> container</param>
        /// <param name="log">Application log</param>
        public Bus(Container container, ILog log)
        {
            _container = container;
            _log = log;
            _commandDispatcher = new CommandDispatcher(
                HandleCommand, 
                new DataflowOptions { RecommendedParallelismIfMultiThreaded = 1 });
        }

        /// <inheritdoc />
        public async Task<Task> CommandAsync(ICommand command)
        {
            var tracked = new Tracked<ICommand>(command);
            await _commandDispatcher.SendAsync(tracked);
            return tracked.Task;
        }

        /// <inheritdoc />
        public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
        {
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
                
            dynamic handler = GetInstance(handlerType);
            if (handler != null)
                return await handler.HandleAsync(query as dynamic);

            return default(TResult);            
        }
        
        private object GetInstance(Type type)
        {
            try
            {
                var instance = _container.GetInstance(type);
                return instance;
            }
            catch (Exception e)
            {
                _log.Errors.Add(e); 
                if (e is ActivationException)
                    return null;
                throw;
            }
        }
        
        private async Task HandleCommand(ICommand command)
        {
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
            dynamic handler = GetInstance(handlerType);
            if (handler != null)
                await handler.Handle(command as dynamic).ConfigureAwait(false);
        }

        private class CommandDispatcher : ParallelDataDispatcher<string, Tracked<ICommand>>
        {
            private readonly Func<ICommand, Task> _handler;

            public CommandDispatcher(Func<ICommand, Task> handler, DataflowOptions options) 
                : base(c => c.Value.Target, options, CancellationToken.None)
            {
                _handler = handler;
            }
            
            protected override Dataflow<Tracked<ICommand>> CreateChildFlow(string target)
            {
                var block = new ActionBlock<Tracked<ICommand>>(
                    async c =>
                    {
                        await _handler(c.Value);
                        c.Complete();
                    }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 } );
                
                return block.ToDataflow();
            }
        }
    }
}