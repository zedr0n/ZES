using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class SagaHandler<TSaga> : ISagaHandler<TSaga>
        where TSaga : class, ISaga, new()
    {
        private readonly IMessageQueue _messageQueue;
        private readonly SagaDispatcher _dispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="SagaHandler{TSaga}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="dispatcher">Dispatcher factory</param>
        public SagaHandler(IMessageQueue messageQueue, SagaDispatcher dispatcher)
        {
            _messageQueue = messageQueue;
            _dispatcher = dispatcher;

            var source = new CancellationTokenSource();
            _messageQueue.Messages
                .Where(e => new TSaga().SagaId(e) != null)
                .Do(e =>
                {
                    _messageQueue.UncompleteCommand(e.AncestorId).Wait();
                    _messageQueue.UncompleteCommand(e.RetroactiveId).Wait();
                    _messageQueue.UncompleteMessage(e).Wait();
                })
                .Subscribe(_dispatcher.InputBlock.AsObserver(), source.Token);
            
            _dispatcher.CompletionTask.ContinueWith(t => source.Cancel());
        }
        
        /// <inheritdoc />
        public class SagaDispatcher : ParallelDataDispatcher<string, IEvent>
        {
            private readonly IFactory<SagaFlow> _sagaFlow;

            /// <summary>
            /// Initializes a new instance of the <see cref="SagaDispatcher"/> class.
            /// </summary>
            /// <param name="log">Log helper</param>
            /// <param name="sagaFlow">Fluent builder</param>
            public SagaDispatcher(ILog log, IFactory<SagaFlow> sagaFlow)
                : base(e => new TSaga().SagaId(e), Configuration.DataflowOptions, CancellationToken.None, typeof(TSaga))
            {
                Log = log;
                _sagaFlow = sagaFlow;
            }

            /// <inheritdoc />
            protected override Dataflow<IEvent> CreateChildFlow(string sagaId)
                => _sagaFlow.Create();

            /// <inheritdoc />
            protected override void CleanUp(Exception dataflowException)
            {
                Log?.Errors.Add(dataflowException?.InnerException);
                Log?.Fatal($"SagaHandler<{typeof(TSaga)}> failed");  
            }
        }
        
        /// <inheritdoc />
        public class SagaFlow : Dataflow<IEvent>
        {
            private readonly IEsRepository<ISaga> _repository;
            private readonly ILog _log;

            /// <summary>
            /// Initializes a new instance of the <see cref="SagaFlow"/> class.
            /// </summary>
            /// <param name="repository">Saga repository</param>
            /// <param name="log">Log service</param>
            /// <param name="messageQueue">Message queue service</param>
            public SagaFlow(IEsRepository<ISaga> repository, ILog log, IMessageQueue messageQueue)
                : base(Configuration.DataflowOptions)
            {
                _repository = repository; 
                _log = log;

                var block = new ActionBlock<IEvent>(
                    async e =>
                    {
                        await Handle(e);
                        await messageQueue.CompleteCommand(e.RetroactiveId);
                        await messageQueue.CompleteCommand(e.AncestorId);
                        await messageQueue.CompleteMessage(e);
                    }, DataflowOptions.ToDataflowBlockOptions(false)); // .ToExecutionBlockOption());
            
                RegisterChild(block);
                InputBlock = block;
            }

            /// <inheritdoc />
            public override ITargetBlock<IEvent> InputBlock { get; }

            private async Task Handle(IEvent e)
            {
                // _log.Trace($"{typeof(TSaga).Name}.When({e.EventType}[{e.Stream}])");
                _log.Trace($"{e.MessageType}", typeof(TSaga).GetFriendlyName());
            
                try
                {
                    var emptySaga = new TSaga();
                    var id = emptySaga.SagaId(e);
                    var isInitializer = emptySaga.IsInitializer(e);
                    TSaga saga;
                    if (isInitializer)
                        saga = await _repository.GetOrAdd<TSaga>(id);
                    else
                        saga = await _repository.Find<TSaga>(id);
                    
                    if (saga == null)
                        return;
                    var saveEvent = e.Copy(); 
                    
                    saga.When(saveEvent);
                    
                    var commands = saga.GetUncommittedCommands().OfType<Command>();
                    foreach (var c in commands)
                    {
                        c.AncestorId = e.AncestorId ?? e.MessageId;
                        c.RetroactiveId = e.RetroactiveId;
                        c.CorrelationId = id;
                    }

                    await _repository.Save(saga);
                }
                catch (Exception exception)
                {
                    _log.Errors.Add(exception);
                }
            }
        }
    }
}