using System;
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
        private readonly SagaDispatcher.Builder _dispatcher;

        private CancellationTokenSource _source;

        /// <summary>
        /// Initializes a new instance of the <see cref="SagaHandler{TSaga}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="dispatcher">Dispatcher factory</param>
        public SagaHandler(IMessageQueue messageQueue, SagaDispatcher.Builder dispatcher)
        {
            _messageQueue = messageQueue;
            _dispatcher = dispatcher;

            Start();
        }

        private void Start()
        {
            _source?.Cancel();
            _source = new CancellationTokenSource();
            
            var dispatcher = _dispatcher
                .WithOptions(new DataflowOptions { RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance })
                .Bind(); 

            _messageQueue.Messages.Select(e =>
            {
                _messageQueue.UncompleteMessage(e).Wait();
                var tracked = new Tracked<IEvent>(e);
                tracked.Task.ContinueWith(t => _messageQueue.CompleteMessage(e));
                return tracked;
            }).SubscribeOn(Scheduler.Default).Subscribe(dispatcher.InputBlock.AsObserver(), _source.Token);
            dispatcher.CompletionTask.ContinueWith(t => _source.Cancel());
        }

        /// <inheritdoc />
        public class SagaDispatcher : ParallelDataDispatcher<string, Tracked<IEvent>>
        {
            private readonly SagaFlow.Builder _sagaFlow;

            /// <summary>
            /// Initializes a new instance of the <see cref="SagaDispatcher"/> class.
            /// </summary>
            /// <param name="options">Dataflow options</param>
            /// <param name="log">Log helper</param>
            /// <param name="sagaFlow">Fluent builder</param>
            private SagaDispatcher(DataflowOptions options, ILog log, SagaFlow.Builder sagaFlow)
                : base(e => new TSaga().SagaId(e.Value), options, CancellationToken.None, typeof(TSaga))
            {
                Log = log;
                _sagaFlow = sagaFlow;
            }

            /// <inheritdoc />
            protected override Dataflow<Tracked<IEvent>> CreateChildFlow(string sagaId)
                => _sagaFlow.WithOptions(DataflowOptions).Bind(sagaId);

            /// <inheritdoc />
            protected override void CleanUp(Exception dataflowException)
            {
                Log?.Errors.Add(dataflowException?.InnerException);
                Log?.Fatal($"SagaHandler<{typeof(TSaga)}> failed");  
            }

            /// <inheritdoc />
            public class Builder : FluentBuilder
            {
                private readonly ILog _log;
                private readonly SagaFlow.Builder _sagaFlow;
                private DataflowOptions _options = DataflowOptions.Default;

                /// <summary>
                /// Initializes a new instance of the <see cref="Builder"/> class.
                /// </summary>
                /// <param name="log">Log helper</param>
                /// <param name="sagaFlow">Saga flow builder</param>
                public Builder(ILog log, SagaFlow.Builder sagaFlow)
                {
                    _log = log;
                    _sagaFlow = sagaFlow;
                }

                internal Builder WithOptions(DataflowOptions options)
                    => Clone(this, b => b._options = options);

                internal SagaDispatcher Bind() =>
                    new SagaDispatcher(_options, _log, _sagaFlow);
            }

            /// <inheritdoc />
            public class SagaFlow : Dataflow<Tracked<IEvent>>
            {
                private readonly IEsRepository<ISaga> _repository;
                private readonly ILog _log;

                private readonly string _id;

                private SagaFlow(DataflowOptions options, string id, IEsRepository<ISaga> repository, ILog log)
                    : base(options)
                {
                    _id = id;
                    _repository = repository; 
                    _log = log;

                    var block = new ActionBlock<Tracked<IEvent>>(
                        async e =>
                        { 
                            await Handle(e.Value);
                            e.Complete();
                        }, options.ToExecutionBlockOption());
                
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
                        var saga = await _repository.GetOrAdd<TSaga>(_id);
                        if (saga == null)
                            return;
                        var saveEvent = ((Event)e).Copy(); 
                        
                        saga.When(saveEvent);
                        
                        var commands = saga.GetUncommittedCommands().OfType<Command>();
                        foreach (var c in commands)
                            c.AncestorId = e.MessageId;
                        
                        await _repository.Save(saga);
                    }
                    catch (Exception exception)
                    {
                        _log.Errors.Add(exception);
                    }
                }

                /// <inheritdoc />
                public class Builder : FluentBuilder
                {
                    private readonly IEsRepository<ISaga> _repository;
                    private readonly ILog _log;

                    private DataflowOptions _options;

                    /// <summary>
                    /// Initializes a new instance of the <see cref="Builder"/> class.
                    /// </summary>
                    /// <param name="repository">Saga repository</param>
                    /// <param name="log">Log helper</param>
                    public Builder(IEsRepository<ISaga> repository, ILog log)
                    {
                        _repository = repository;
                        _log = log;
                    }

                    internal Builder WithOptions(DataflowOptions options)
                        => Clone(this, b => b._options = options);

                    internal SagaFlow Bind(string sagaId) =>
                        new SagaFlow(_options, sagaId, _repository, _log);
                }
            } 
        }
    }
}