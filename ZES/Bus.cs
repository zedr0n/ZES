using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using SimpleInjector;
using ZES.Infrastructure;
using ZES.Infrastructure.Alerts;
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
        private readonly ConcurrentDictionary<string, CommandDispatcher> _dispatchers = new ConcurrentDictionary<string, CommandDispatcher>();
        private readonly ILog _log;
        private readonly ITimeline _timeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bus"/> class.
        /// </summary>
        /// <param name="container"><see cref="SimpleInjector"/> container</param>
        /// <param name="log">Application log</param>
        /// <param name="timeline">Timeline</param>
        /// <param name="messageQueue">Message queue</param>
        public Bus(Container container, ILog log, ITimeline timeline, IMessageQueue messageQueue)
        {
            _container = container;
            _log = log;
            _timeline = timeline;

            messageQueue.Alerts.OfType<BranchDeleted>().Subscribe(e => DeleteDispatcher(e.BranchId));
        }
        
        /// <inheritdoc />
        public async Task<Task> CommandAsync(ICommand command)
        {
            var tracked = new Tracked<ICommand>(command);
            var dispatcher = _dispatchers.GetOrAdd(_timeline.Id, CreateDispatcher);
            await dispatcher.SendAsync(tracked);

            return tracked.Task;
        }

        /// <inheritdoc />
        public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
        {
            var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
                
            var handler = (IQueryHandler)GetInstance(handlerType);
            if (handler != null)
                return (TResult)await handler.HandleAsync(query);

            return default(TResult);            
        }
        
        private void DeleteDispatcher(string timeline)
        {
            _dispatchers.TryRemove(timeline, out var dispatcher);
        }

        private CommandDispatcher CreateDispatcher(string timeline) => new CommandDispatcher(
            HandleCommand, 
            timeline, 
            new DataflowOptions { RecommendedParallelismIfMultiThreaded = 1 }); 

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

        private class CommandDispatcher : ParallelDataDispatcher<string, Tracked<ICommand>>
        {
            private readonly Func<ICommand, Task> _handler;

            public CommandDispatcher(Func<ICommand, Task> handler, string timeline, DataflowOptions options) 
                : base(c => $"{timeline}:{c.Value.Target}", options, CancellationToken.None)
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