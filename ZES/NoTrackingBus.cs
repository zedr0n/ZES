using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SimpleInjector;
using ZES.Infrastructure;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

#pragma warning disable CS4014

namespace ZES
{
    /// <inheritdoc />
    public class NoTrackingBus : IBus
    {
        private readonly Container _container;

        private readonly ConcurrentDictionary<string, CommandDispatcher> _dispatchers = new ConcurrentDictionary<string, CommandDispatcher>();
        private readonly ConcurrentDictionary<Type, IQueryHandler> _queryHandlers = new ConcurrentDictionary<Type, IQueryHandler>();
        private readonly ILog _log;
        private readonly ITimeline _timeline;
        private readonly IMessageQueue _messageQueue;
        private readonly ICommandLog _commandLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="NoTrackingBus"/> class.
        /// </summary>
        /// <param name="container"><see cref="SimpleInjector"/> container</param>
        /// <param name="log">Application log</param>
        /// <param name="timeline">Timeline</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="commandLog">Command log</param>
        public NoTrackingBus(Container container, ILog log, ITimeline timeline, IMessageQueue messageQueue, ICommandLog commandLog)
        {
            _container = container;
            _log = log;
            _timeline = timeline;
            _messageQueue = messageQueue;
            _commandLog = commandLog;

            messageQueue.Alerts.OfType<BranchDeleted>().Subscribe(e => DeleteDispatcher(e.BranchId));
        }

        /// <inheritdoc />
        public async Task<bool> Command(ICommand command, int nRetries = 0)
        {
            await await CommandAsync(command);
            var failedCommands = await _commandLog.FailedCommands.FirstAsync();
            while (nRetries-- > 0)
            {
                if (failedCommands.All(c => c.MessageId != command.MessageId))
                    return true;
            
                _log.Warn($"Retrying command {command.GetType().GetFriendlyName()}");
                await await CommandAsync(command);
                failedCommands = await _commandLog.FailedCommands.FirstAsync();
            }

            return failedCommands.Any(c => c.MessageId == command.MessageId);
        }
       
        /// <inheritdoc />
        public async Task<Task> CommandAsync(ICommand command)
        {
            if (command.GetType().IsClosedTypeOf(typeof(RetroactiveCommand<>)))
                await _messageQueue.RetroactiveExecution.FirstAsync(b => b == false).Timeout(Configuration.Timeout);
            command.Timeline = _timeline.Id;
            if (!command.Pure)
                await _messageQueue.UncompleteMessage(command);
           
            var dispatcher = _dispatchers.GetOrAdd(_timeline.Id, CreateDispatcher);
            await dispatcher.SendAsync(command);

            var task = dispatcher.OutputBlock.ReceiveAsync(item => item.Equals(command));

            if (command.Pure)
                return task; 
            
            return task.ContinueWith(_ => _messageQueue.CompleteMessage(command)).Unwrap();
        }
        
        /// <inheritdoc />
        public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
        {
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));

            // var handler = (IQueryHandler)GetInstance(handlerType);
            var handler = _queryHandlers.GetOrAdd(handlerType, t => (IQueryHandler)GetInstance(t));
            if (handler != null)
                return (TResult)await handler.Handle(query);

            return default(TResult);            
        }
        
        private void DeleteDispatcher(string timeline)
        {
            _dispatchers.TryRemove(timeline, out var dispatcher);
        }

        private CommandDispatcher CreateDispatcher(string timeline) => new CommandDispatcher(
            HandleCommand,
            timeline,
            Configuration.DataflowOptions);

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
            var handler = (ICommandHandler)GetInstance(handlerType);
            if (handler != null)
                await handler.Handle(command).ConfigureAwait(false);
        }

        private class CommandDispatcher : ParallelDataDispatcherWithOutput<string, ICommand>
        {
            private readonly Func<ICommand, Task> _handler;
            private readonly DataflowOptions _options;

            public CommandDispatcher(Func<ICommand, Task> handler, string timeline, DataflowOptions options) 
                : base(c => $"{timeline}:{c.Target}", options, CancellationToken.None)
            {
                _handler = handler;
                _options = options;
            }
            
            protected override Dataflow<ICommand, ICommand> CreateChildFlow(string target)
            {
                var block = new TransformBlock<ICommand, ICommand>(
                    async c =>
                    {
                        await _handler(c);
                        return c;
                    }, _options.ToDataflowBlockOptions()); 
                
                return block.ToDataflow(_options);
            }
        }
    }
}