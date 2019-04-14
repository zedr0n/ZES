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
        private readonly ILog _log;


        private class CommandFlow : Dataflow<ICommand>
        {
            private readonly ActionBlock<ICommand> _inputBlock;
            private TaskCompletionSource<bool> _next = new TaskCompletionSource<bool>();
            
            public CommandFlow(Func<ICommand,Task> handler) : base(DataflowOptions.Default)
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
            
            public CommandProcessor(Func<ICommand,Task> handler) : base(DataflowOptions.Default)
            {
                _commandBlock = new ActionBlock<ICommand>(async c =>
                {
                    var flow = _flows.GetOrAdd(c.Target, new CommandFlow(handler));
                    await flow.SendAsync(c);
                    await flow.Next;
                }, new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 8
                });
                
                RegisterChild(_commandBlock);
            }

            public override ITargetBlock<ICommand> InputBlock => _commandBlock;
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
                //_log.WriteLine("Failed to create handler " + type.Name );
                _log.Error(e.Message,this);
                if (e is ActivationException)
                    return null;
                throw;
            }
            
        }
        
        public Bus(Container container, ILog log)
        {
            _container = container;
            _log = log;
            _commandProcessor = new CommandProcessor(HandleCommand); 
        }

        private async Task HandleCommand(ICommand command)
        {
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
            dynamic handler = GetInstance(handlerType);
            if (handler != null)
                await handler.Handle(command as dynamic);
        }

        public async Task<bool> CommandAsync(ICommand command)
        {
            return await _commandProcessor.InputBlock.SendAsync(command);
        }

        public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
        {
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
                
            dynamic handler = GetInstance(handlerType);
            if (handler != null)
                return await handler.HandleAsync(query as dynamic);

            return default(TResult);            
        }

    }
}