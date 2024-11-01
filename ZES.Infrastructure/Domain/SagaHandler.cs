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
        /// <summary>
        /// Initializes a new instance of the <see cref="SagaHandler{TSaga}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="dispatcher">Dispatcher factory</param>
        /// <param name="flowCompletionService">Flow completion service</param>
        public SagaHandler(IMessageQueue messageQueue, SagaDispatcher dispatcher, IFlowCompletionService flowCompletionService)
        {

            var source = new CancellationTokenSource();
            messageQueue.Messages
                .Where(e => new TSaga().SagaId(e) != null)
                .TakeWhile(_ => !source.Token.IsCancellationRequested)
                .Select(e => 
                {
                    flowCompletionService.TrackMessage(e);
                    return e;
                })
                .Select(e => Observable.FromAsync(async _ =>
                {
                    await dispatcher.SubmitAsync(e);
                }))
                .Concat()
                .Subscribe();
            
            dispatcher.CompletionTask.ContinueWith(t => source.Cancel());
        }
        
        /// <inheritdoc />
        public class SagaDispatcher : ParallelDataDispatcher<string, IEvent>
        {
            private readonly IFactory<SagaFlow> _sagaFlow;

            private readonly BroadcastBlock<IEvent> _broadcastBlock;
            private readonly BufferBlock<IEvent> _bufferBlock;

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
                _broadcastBlock = new BroadcastBlock<IEvent>(null);
                
                _bufferBlock = new BufferBlock<IEvent>();
                _broadcastBlock.LinkTo(_bufferBlock);
                _broadcastBlock.LinkTo(DispatcherBlock);
            }
            
            /// <summary>
            /// Asynchronously submit the event to saga and wait for it to be recorded
            /// </summary>
            /// <param name="e">Event for the saga</param>
            public async Task SubmitAsync(IEvent e)
            {
                await this.SendAsync(e);
                await _bufferBlock.ReceiveAsync(x => x.MessageId == e.MessageId);
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

            /// <inheritdoc />
            public override ITargetBlock<IEvent> InputBlock => _broadcastBlock;
        }
        
        /// <inheritdoc />
        public class SagaFlow : Dataflow<IEvent>
        {
            private readonly IEsRepository<ISaga> _repository;
            private readonly ILog _log;
            private readonly IFlowCompletionService _flowCompletionService;

            /// <summary>
            /// Initializes a new instance of the <see cref="SagaFlow"/> class.
            /// </summary>
            /// <param name="repository">Saga repository</param>
            /// <param name="log">Log service</param>
            /// <param name="messageQueue">Message queue service</param>
            /// <param name="flowCompletionService">Flow completion service</param>
            public SagaFlow(IEsRepository<ISaga> repository, ILog log, IMessageQueue messageQueue, IFlowCompletionService flowCompletionService)
                : base(Configuration.DataflowOptions)
            {
                _repository = repository; 
                _log = log;
                _flowCompletionService = flowCompletionService;

                var block = new ActionBlock<IEvent>(
                    async e =>
                    {
                        await Handle(e);
                        flowCompletionService.MarkComplete(e);
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
                    var saga = await _repository.GetOrAdd<TSaga>(id);
                    
                    if (saga == null)
                        return;
                    var saveEvent = saga.ToSagaEvent(e); 
                    
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