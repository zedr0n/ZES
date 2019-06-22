using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Dataflow;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Sagas
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
                .WithOptions(new DataflowOptionsEx { RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance })
                .Bind(); 

            _messageQueue.Messages.Subscribe(dispatcher.InputBlock.AsObserver(), _source.Token);
            dispatcher.CompletionTask.ContinueWith(t => _source.Cancel());
        }

        /// <inheritdoc />
        public class SagaDispatcher : ParallelDataDispatcher<string, IEvent, Task>
        {
            private readonly SagaFlow.Builder _sagaFlow;

            /// <summary>
            /// Initializes a new instance of the <see cref="SagaDispatcher"/> class.
            /// </summary>
            /// <param name="options">Dataflow options</param>
            /// <param name="log">Log helper</param>
            /// <param name="sagaFlow">Fluent builder</param>
            private SagaDispatcher(DataflowOptions options, ILog log, SagaFlow.Builder sagaFlow)
                : base(e => new TSaga().SagaId(e), options, CancellationToken.None, typeof(TSaga))
            {
                Log = log;
                _sagaFlow = sagaFlow;
            }

            /// <inheritdoc />
            protected override Dataflow<IEvent, Task> CreateChildFlow(string sagaId)
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
            public class SagaFlow : Dataflow<IEvent, Task>
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

                    var block = new TransformBlock<IEvent, Task>(
                        async e =>
                        { 
                            var task = Handle(e);
                            await task;
                            return task;
                        }, options.ToExecutionBlockOption());
                
                    RegisterChild(block);
                    InputBlock = block;
                    OutputBlock = block;
                }

                /// <inheritdoc />
                public override ITargetBlock<IEvent> InputBlock { get; }

                /// <inheritdoc />
                public override ISourceBlock<Task> OutputBlock { get; }

                private async Task Handle(IEvent e)
                {
                    // _log.Trace($"{typeof(TSaga).Name}.When({e.EventType}[{e.Stream}])");
                    _log.Trace($"{e.EventType}", typeof(TSaga).GetFriendlyName());
                
                    try
                    {
                        var saga = await _repository.GetOrAdd<TSaga>(_id);
                        if (saga == null)
                            return;
                        var saveEvent = ((Event)e).Copy(); 
                        
                        saga.When(saveEvent);
                        await _repository.Save(saga, e.MessageId);
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