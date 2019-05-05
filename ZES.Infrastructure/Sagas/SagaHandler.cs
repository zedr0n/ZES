using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Interfaces;
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
                .WithOptions(new DataflowOptionsEx{ RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance })
                .Bind(); 

            var obs = _messageQueue.Messages;
            obs.Subscribe(
                async e =>
                {
                    try
                    { 
                        await dispatcher.SendAsync(e);    
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }, _source.Token);
            dispatcher.CompletionTask.ContinueWith(t =>
            {
                _source.Cancel();
            });
        }

        public class SagaDispatcher : ParallelDataDispatcher<string, IEvent, Task>
        {
            private readonly ILog _log;
            private readonly IErrorLog _errorLog;

            private readonly SagaFlow.Builder _sagaFlow;

            /// <summary>
            /// Initializes a new instance of the <see cref="SagaDispatcher"/> class.
            /// </summary>
            /// <param name="options">Dataflow options</param>
            /// <param name="log">Log helper</param>
            /// <param name="errorLog">Error log helper</param>
            /// <param name="sagaFlow">Fluent builder</param>
            private SagaDispatcher(DataflowOptions options, ILog log, IErrorLog errorLog, SagaFlow.Builder sagaFlow)
                : base(e => new TSaga().SagaId(e), options, typeof(TSaga))
            {
                _log = log;
                _errorLog = errorLog;
                _sagaFlow = sagaFlow;
            }

            protected override Dataflow<IEvent, Task> CreateChildFlow(string sagaId)
                => _sagaFlow.WithOptions(DataflowOptions).Bind(sagaId);

            protected override void CleanUp(Exception dataflowException)
            {
                _errorLog.Add(dataflowException.InnerException);
                _log.Fatal($"SagaHandler<{typeof(TSaga)}> failed");  
            }

            public class Builder
            {
                private readonly ILog _log;
                private readonly IErrorLog _errorLog;
                private readonly SagaFlow.Builder _sagaFlow;
                private DataflowOptions _options; 

                public Builder(ILog log, IErrorLog errorLog, SagaFlow.Builder sagaFlow)
                {
                    _log = log;
                    _errorLog = errorLog;
                    _sagaFlow = sagaFlow;
                    Reset();
                }

                public Builder WithOptions(DataflowOptions options)
                {
                    _options = options;
                    return this;
                }

                public SagaDispatcher Bind()
                {
                    var dispatcher = new SagaDispatcher(_options, _log, _errorLog, _sagaFlow);
                    Reset();
                    return dispatcher;
                }

                private void Reset()
                {
                    _options = DataflowOptions.Default;
                }
            }
            
            public class SagaFlow : Dataflow<IEvent, Task>
            {
                private readonly IEsRepository<ISaga> _repository;
                private readonly ILog _log;
                private readonly IErrorLog _errorLog;

                private readonly string _id;

                private SagaFlow(DataflowOptions options, string id, IEsRepository<ISaga> repository, ILog log, IErrorLog errorLog)
                    : base(options)
                {
                    _id = id;
                    _repository = repository; 
                    _log = log;
                    _errorLog = errorLog;

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

                public override ITargetBlock<IEvent> InputBlock { get; }
                public override ISourceBlock<Task> OutputBlock { get; }

                private async Task Handle(IEvent e)
                {
                    _log.Trace($"{typeof(TSaga).Name}::When({e.EventType}[{e.Stream}])");
                
                    try
                    {
                        var saga = await _repository.GetOrAdd<TSaga>(_id);  
                        saga?.When(e);
                        await _repository.Save(saga);
                    }
                    catch (Exception exception)
                    {
                        _errorLog.Add(exception);
                    }
                }

                public class Builder
                {
                    private readonly IEsRepository<ISaga> _repository;
                    private readonly ILog _log;
                    private readonly IErrorLog _errorLog;

                    private DataflowOptions _options;

                    public Builder(IEsRepository<ISaga> repository, ILog log, IErrorLog errorLog)
                    {
                        _repository = repository;
                        _log = log;
                        _errorLog = errorLog;
                        
                        Reset();
                    }

                    public Builder WithOptions(DataflowOptions options)
                    {
                        _options = options;
                        return this;
                    }

                    public SagaFlow Bind(string sagaId)
                    {
                        var flow = new SagaFlow(_options, sagaId, _repository, _log, _errorLog);
                        Reset();
                        return flow;
                    }

                    private void Reset()
                    {
                        _options = DataflowOptions.Default;
                    }
                }
            } 
        }
    }
}