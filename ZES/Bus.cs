using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SimpleInjector;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES
{
    public class Bus : IBus
    {
        private readonly Container _container;
        private readonly CommandProcessor _commandProcessor;
        private readonly ConcurrentDictionary<ICommand, TaskCompletionSource<bool>> _executing = new ConcurrentDictionary<ICommand, TaskCompletionSource<bool>>();
        private readonly IErrorLog _errorLog;

        public Bus(Container container, IErrorLog errorLog)
        {
            _container = container;
            _errorLog = errorLog;
            _commandProcessor = new CommandProcessor(HandleCommand, _executing); 
        }

        public async Task<Task> CommandAsync(ICommand command)
        {
            var source = new TaskCompletionSource<bool>();
            _executing[command] = source;
            if (!await _commandProcessor.InputBlock.SendAsync(command))
                return Task.FromResult(false);
            return source.Task;
        }

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
                await handler.Handle(command as dynamic);
        }
        
        private class CommandFlow : Dataflow<ICommand>
        {
            private readonly ActionBlock<ICommand> _inputBlock;
            private TaskCompletionSource<bool> _next = new TaskCompletionSource<bool>();
            
            public CommandFlow(Func<ICommand, Task> handler)
                : base(DataflowOptions.Default)
            {
                _inputBlock = new ActionBlock<ICommand>(async c =>
                {
                    await handler(c);
                    _next.SetResult(true);
                    _next = new TaskCompletionSource<bool>();
                });
                RegisterChild(_inputBlock);
            }

            public override ITargetBlock<ICommand> InputBlock => _inputBlock;
            public Task Next => _next.Task;
        }
                        
        private class CommandProcessor : Dataflow<ICommand>
        {
            private readonly ActionBlock<ICommand> _commandBlock;
            private readonly ConcurrentDictionary<string, CommandFlow> _flows = new ConcurrentDictionary<string, CommandFlow>();
            private readonly ConcurrentDictionary<ICommand, TaskCompletionSource<bool>> _executing;
            
            public CommandProcessor(Func<ICommand, Task> handler, ConcurrentDictionary<ICommand, TaskCompletionSource<bool>> executing)
                : base(DataflowOptions.Default)
            {
                _executing = executing;
                _commandBlock = new ActionBlock<ICommand>(
                    async c =>
                    {
                        var flow = _flows.GetOrAdd(c.Target, new CommandFlow(handler));
                        await flow.SendAsync(c);
                        await flow.Next;            
                        _executing.TryRemove(c, out var source);
                        source.SetResult(true);
                    }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 8 });
                
                RegisterChild(_commandBlock);
            }

            public override ITargetBlock<ICommand> InputBlock => _commandBlock;
        }
    }
}