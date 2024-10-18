using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Concurrency;
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
using AsyncLock = NeoSmart.AsyncLock.AsyncLock;

#pragma warning disable CS4014

namespace ZES
{
    /// <inheritdoc />
    public class Bus : IBus
    {
        private readonly Container _container;

        private readonly ConcurrentDictionary<string, CommandDispatcher> _dispatchers = new(); 
        private readonly ConcurrentDictionary<Type, IQueryHandler> _queryHandlers = new();
        private readonly ILog _log;
        private readonly ITimeline _timeline;
        private readonly IMessageQueue _messageQueue;
        private readonly ICommandLog _commandLog;
        private readonly IFlowCompletionService _flowCompletionService;
        private readonly AsyncLock _lock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Bus"/> class.
        /// </summary>
        /// <param name="container"><see cref="SimpleInjector"/> container</param>
        /// <param name="log">Application log</param>
        /// <param name="timeline">Timeline</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="commandLog">Command log</param>
        /// <param name="flowCompletionService">Flow completion service</param>
        public Bus(Container container, ILog log, ITimeline timeline, IMessageQueue messageQueue, ICommandLog commandLog, IFlowCompletionService flowCompletionService)
        {
            _container = container;
            _log = log;
            _timeline = timeline;
            _messageQueue = messageQueue;
            _commandLog = commandLog;
            _flowCompletionService = flowCompletionService;

            messageQueue.Alerts.OfType<BranchDeleted>().Subscribe(e => DeleteDispatcher(e.BranchId));
            
            flowCompletionService.RetroactiveExecution.DistinctUntilChanged()
                .Throttle(x => Observable.Timer(TimeSpan.FromMilliseconds(x ? 0 : 10)))
                .StartWith(false)
                .DistinctUntilChanged()
                .Subscribe(x => _log.Info($"Retroactive execution : {x}"));
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
            command.Timeline = _timeline.Id;
            if (command is IRetroactiveCommand)
                await _flowCompletionService.CompletionAsync();//.Timeout(Configuration.Timeout);
            else if (command.RetroactiveId == default && command.AncestorId == default)
                await _flowCompletionService.RetroactiveExecution.FirstAsync(b => b == false);

            _flowCompletionService.TrackMessage(command);
            
            var tracked = new Tracked<ICommand>(command);
            var dispatcher = _dispatchers.GetOrAdd(_timeline.Id, CreateDispatcher);
            await dispatcher.SubmitAsync(tracked);

            var completionTask = _flowCompletionService.NodeCompletionAsync(command);
            return completionTask;
        }
        
        /// <inheritdoc />
        public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
        {
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));

            // var handler = (IQueryHandler)GetInstance(handlerType);
            var handler = _queryHandlers.GetOrAdd(handlerType, t => (IQueryHandler)GetInstance(t));
            if (handler != null)
                return (TResult)await handler.Handle(query);

            return default;            
        }
        
        private void DeleteDispatcher(string timeline)
        {
            _dispatchers.TryRemove(timeline, out var dispatcher);
        }

        private CommandDispatcher CreateDispatcher(string timeline) => new CommandDispatcher(
                GetHandler,
                _flowCompletionService,
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

        private ICommandHandler GetHandler(ICommand command)
        {
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
            var handler = (ICommandHandler)GetInstance(handlerType);
            return handler;
        }

        private class CommandDispatcher : ParallelDataDispatcher<string, Tracked<ICommand>>
        {
            private readonly Func<ICommand, ICommandHandler> _handler;
            private readonly IFlowCompletionService _flowCompletionService;
            private readonly DataflowOptions _options;

            private readonly BufferBlock<Tracked<ICommand>> _bufferBlock;
            private readonly BroadcastBlock<Tracked<ICommand>> _broadcastBlock;
            
            public CommandDispatcher(Func<ICommand, ICommandHandler> handler, IFlowCompletionService flowCompletionService, string timeline, DataflowOptions options) 
                : base(c => $"{timeline}:{c.Value.Target}", options, CancellationToken.None)
            {
                _handler = handler;
                _flowCompletionService = flowCompletionService;
                _options = options;

                _broadcastBlock = new BroadcastBlock<Tracked<ICommand>>(null);
                _bufferBlock = new BufferBlock<Tracked<ICommand>>();
                
                _broadcastBlock.LinkTo(_bufferBlock);
                _broadcastBlock.LinkTo(DispatcherBlock);
            }

            public async Task SubmitAsync(Tracked<ICommand> command)
            {
                await this.SendAsync(command);
                await _bufferBlock.ReceiveAsync(c => c.Value.MessageId == command.Value.MessageId);
            }
           
            protected override Dataflow<Tracked<ICommand>> CreateChildFlow(string target)
            {
                var block = new ActionBlock<Tracked<ICommand>>(
                    async c =>
                    {
                        await _handler(c.Value).Handle(c.Value);
                        _flowCompletionService.MarkComplete(c.Value);
                        c.Complete();
                        
                    }, _options.ToDataflowBlockOptions());
                
                return block.ToDataflow(_options);
            }

            public override ITargetBlock<Tracked<ICommand>> InputBlock => _broadcastBlock;
        }
    }
}