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
        private readonly IErrorLog _errorLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bus"/> class.
        /// </summary>
        /// <param name="container"><see cref="SimpleInjector"/> container</param>
        /// <param name="errorLog">Application error log</param>
        public Bus(Container container, IErrorLog errorLog)
        {
            _container = container;
            _errorLog = errorLog;
            _commandDispatcher = new CommandDispatcher(
                HandleCommand, 
                new DataflowOptions { RecommendedParallelismIfMultiThreaded = 1 });
        }

        /// <inheritdoc />
        public async Task<Task> CommandAsync(ICommand command)
        {
            await _commandDispatcher.SendAsync(command);
            return _commandDispatcher.ReceiveAsync(command);
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
                _errorLog.Add(e); 
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

        private class CommandFlow : Dataflow<ICommand, Task>
        {
            public CommandFlow(Func<ICommand, Task> handler, DataflowOptions options)
                : base(options)
            {
                var outputBlock = new BufferBlock<Task>();
                var inputBlock = new ActionBlock<ICommand>(
                    async c =>
                    {
                        var task = handler(c);
                        await outputBlock.SendAsync(task); 
                        await task; 
                    }, options.ToExecutionBlockOption());
                
                RegisterChild(inputBlock);
                RegisterChild(outputBlock);
                
                InputBlock = inputBlock;
                OutputBlock = outputBlock;
            }

            public override ITargetBlock<ICommand> InputBlock { get; }
            public override ISourceBlock<Task> OutputBlock { get; }
        }
        
        private class CommandDispatcher : ParallelDataDispatcher<string, ICommand, Task>
        {
            private readonly Func<ICommand, Task> _handler;

            public CommandDispatcher(Func<ICommand, Task> handler, DataflowOptions options) 
                : base(c => c.Target, options, CancellationToken.None)
            {
                _handler = handler;
            }
            
            public async Task ReceiveAsync(ICommand command) => await Output(command.Target).ReceiveAsync();

            protected override Dataflow<ICommand, Task> CreateChildFlow(string target)
            {
                return new CommandFlow(_handler, DataflowOptions);
            }
        }
    }
}