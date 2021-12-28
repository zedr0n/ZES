using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
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

        private CancellationTokenSource _source;

        /// <summary>
        /// Initializes a new instance of the <see cref="SagaHandler{TSaga}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="dispatcher">Dispatcher factory</param>
        public SagaHandler(IMessageQueue messageQueue, SagaDispatcher dispatcher)
        {
            _messageQueue = messageQueue;
            _dispatcher = dispatcher;

            Start();
        }

        private void Start()
        {
            _source?.Cancel();
            _source = new CancellationTokenSource();
            
            _messageQueue.Messages
                .Where(e => new TSaga().SagaId(e) != null).Select(e =>
            {
                _messageQueue.UncompleteMessage(e).Wait();
                var tracked = new Tracked<IEvent>(e);
                tracked.Task.ContinueWith(t => _messageQueue.CompleteMessage(e));
                return tracked;
            }).SubscribeOn(Scheduler.Default).Subscribe(_dispatcher.InputBlock.AsObserver(), _source.Token);
            _dispatcher.CompletionTask.ContinueWith(t => _source.Cancel());
        }

        /// <inheritdoc />
        public class SagaDispatcher : ParallelDataDispatcher<string, Tracked<IEvent>>
        {
            private readonly IFactory<SagaFlow> _sagaFlow;

            /// <summary>
            /// Initializes a new instance of the <see cref="SagaDispatcher"/> class.
            /// </summary>
            /// <param name="log">Log helper</param>
            /// <param name="sagaFlow">Fluent builder</param>
            public SagaDispatcher(ILog log, IFactory<SagaFlow> sagaFlow)
                : base(e => new TSaga().SagaId(e.Value), Configuration.DataflowOptions, CancellationToken.None, typeof(TSaga))
            {
                Log = log;
                _sagaFlow = sagaFlow;
            }

            /// <inheritdoc />
            protected override Dataflow<Tracked<IEvent>> CreateChildFlow(string sagaId)
                => _sagaFlow.Create();

            /// <inheritdoc />
            protected override void CleanUp(Exception dataflowException)
            {
                Log?.Errors.Add(dataflowException?.InnerException);
                Log?.Fatal($"SagaHandler<{typeof(TSaga)}> failed");  
            }
        }
        
        /// <inheritdoc />
        public class SagaFlow : Dataflow<Tracked<IEvent>>
        {
            private readonly IEsRepository<ISaga> _repository;
            private readonly ILog _log;

            /// <summary>
            /// Initializes a new instance of the <see cref="SagaFlow"/> class.
            /// </summary>
            /// <param name="repository">Saga repository</param>
            /// <param name="log">Log service</param>
            public SagaFlow(IEsRepository<ISaga> repository, ILog log)
                : base(Configuration.DataflowOptions)
            {
                _repository = repository; 
                _log = log;

                var block = new ActionBlock<Tracked<IEvent>>(
                    async e =>
                    { 
                        await Handle(e.Value);
                        e.Complete();
                    }, DataflowOptions.ToExecutionBlockOption());
            
                RegisterChild(block);
                InputBlock = block;
            }

            /// <inheritdoc />
            public override ITargetBlock<Tracked<IEvent>> InputBlock { get; }

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
                    var saveEvent = ((Event)e).Copy(); 
                    
                    saga.When(saveEvent);
                    
                    var commands = saga.GetUncommittedCommands().OfType<Command>();
                    foreach (var c in commands)
                    {
                        c.AncestorId = e.MessageId;
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