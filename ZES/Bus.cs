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
        private readonly BranchCommandDispatcher _commandDispatcher;
        private readonly ILog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bus"/> class.
        /// </summary>
        /// <param name="container"><see cref="SimpleInjector"/> container</param>
        /// <param name="log">Application log</param>
        /// <param name="timeline">Timeline</param>
        public Bus(Container container, ILog log, ITimeline timeline)
        {
            _container = container;
            _log = log;
            _commandDispatcher = new BranchCommandDispatcher(
                HandleCommand, 
                _log,
                timeline, 
                new DataflowOptions { RecommendedParallelismIfMultiThreaded = 8 });
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
                
            var handler = (IQueryHandler)GetInstance(handlerType);
            if (handler != null)
                return (TResult)await handler.HandleAsync(query);

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
            var handler = (ICommandHandler)GetInstance(handlerType);
            if (handler != null)
                await handler.Handle(command).ConfigureAwait(false);
        }

        /// <summary>
        /// Command dispatcher parallel by branch
        /// </summary>
        private class BranchCommandDispatcher : ParallelDataDispatcher<string, Tracked<ICommand>>
        {
            private readonly Func<ICommand, Task> _handler;
            private readonly ITimeline _timeline;

            public BranchCommandDispatcher(Func<ICommand, Task> handler, ILog log, ITimeline timeline, DataflowOptions options) 
                : base(c => timeline.Id, options, CancellationToken.None)
            {
                _handler = handler;
                _timeline = timeline;
                Log = log;
            }

            /// <inheritdoc />
            protected override Dataflow<Tracked<ICommand>> CreateChildFlow(string dispatchKey)
            {
                return new CommandDispatcher(_handler, _timeline, new DataflowOptions { RecommendedParallelismIfMultiThreaded = 1 }); 
            }
        }

        private class CommandDispatcher : ParallelDataDispatcher<string, Tracked<ICommand>>
        {
            private readonly Func<ICommand, Task> _handler;

            public CommandDispatcher(Func<ICommand, Task> handler, ITimeline timeline, DataflowOptions options) 
                : base(c => $"{timeline.Id}:{c.Value.Target}", options, CancellationToken.None)
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