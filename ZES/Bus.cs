using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using SimpleInjector;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES
{
    public class Bus : IBus
    {
        private readonly Container _container;
        private readonly ActionBlock<ICommand> _commandProcessor;
        
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
                if (e is ActivationException)
                    return null;
                throw;
            }
            
        }
        
        public Bus(Container container)
        {
            _container = container;
            _commandProcessor = new ActionBlock<ICommand>(HandleCommand,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 8
                });
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

        public bool Command(ICommand command)
        {
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
            dynamic handler = GetInstance(handlerType);
            if (handler == null)
                return false;
            
            handler.Handle(command as dynamic);
            return true;
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
            var res = await _commandProcessor.SendAsync(command);
            if(res)
                _submitted++;
            return res;
        }

        public TResult Query<TResult>(IQuery<TResult> query)
        {
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.Type, typeof(TResult));
            dynamic handler = GetInstance(handlerType);
            if (handler != null)
                try
                {
                    return handler.Handle(query as dynamic);
                }
                catch (Exception e)
                {
                    // ignored
                }

            return default(TResult);            
        }
        public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
        {
            Type handlerType;
            if (query.GetType().GetInterfaces().Contains(typeof(IHistoricalQuery)))
                handlerType = typeof(IHistoricalQueryHandler<,>).MakeGenericType(query.Type, typeof(TResult));
            else
                handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.Type, typeof(TResult));
                
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