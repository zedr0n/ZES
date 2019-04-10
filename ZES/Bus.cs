using System;
using System.Linq;
using System.Reflection;
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

        private class CommandProcessor : Dataflow<ICommand>
        {
            private readonly BufferBlock<ICommand> _inputBlock;
            private readonly ActionBlock<ICommand> _dispatchBlock;
            
            public CommandProcessor(Func<ICommand,Task> handler) : base(DataflowOptions.Default)
            {
                _inputBlock = new BufferBlock<ICommand>();
                
                _dispatchBlock = new ActionBlock<ICommand>(async c => await handler(c), new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 8
                });
                _inputBlock.LinkTo(_dispatchBlock, new DataflowLinkOptions {PropagateCompletion = true});
                
                RegisterChild(_inputBlock);
                RegisterChild(_dispatchBlock);
            }

            public override ITargetBlock<ICommand> InputBlock => _inputBlock;
            public int InputCount => _dispatchBlock.InputCount;
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

        private int _executing;
        private int _submitted;
        public BusStatus Status
        {
            get
            {
                if (_commandProcessor.InputCount > 0)
                    return BusStatus.Busy;
                if (_executing > 0)
                    return BusStatus.Executing;
                if (_submitted > 0)
                    return BusStatus.Submitted;
                return BusStatus.Free;
            }
        }

        private async Task HandleCommand(ICommand command)
        {
            _submitted--;
            _executing++;
            
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
            dynamic handler = GetInstance(handlerType);
            if (handler != null)
                await handler.Handle(command as dynamic);

            _executing--;
        }

        public async Task<bool> CommandAsync(ICommand command)
        {
            var res = await _commandProcessor.InputBlock.SendAsync(command);
            if(res)
                _submitted++;
            return res;
        }

        public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
        {
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
                
            dynamic handler = GetInstance(handlerType);
            if (handler != null)
                try
                {
                    return await handler.HandleAsync(query as dynamic);
                }
                catch (Exception e)
                {
                    // ignored
                }

            return default(TResult);            
        }

    }
}