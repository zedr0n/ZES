using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
        /// <summary>
        /// Initializes a new instance of the <see cref="SagaHandler{TSaga}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="repository">Saga repository</param>
        /// <param name="log">Application log</param>
        /// <param name="errorLog">Application error log</param>
        public SagaHandler(IMessageQueue messageQueue, IEsRepository<ISaga> repository, ILog log, IErrorLog errorLog)
        {
            var dispatcher = new SagaDispatcher(new DataflowOptions { RecommendedParallelismIfMultiThreaded = 8 }, repository, log, errorLog);
            messageQueue.Messages.Subscribe(async e => await dispatcher.SendAsync(e));
        }

        private class SagaDispatcher : ParallelDataDispatcher<IEvent, string>
        {
            private readonly IEsRepository<ISaga> _repository;
            private readonly ILog _log;
            private readonly IErrorLog _errorLog;
            
            private int _parallelCount;
            
            public SagaDispatcher(DataflowOptions options, IEsRepository<ISaga> repository, ILog log, IErrorLog errorLog)
                : base(e => new TSaga().SagaId(e), options)
            {
                _repository = repository;
                _log = log;
                _errorLog = errorLog;
            }

            protected override Dataflow<IEvent> CreateChildFlow(string sagaId) 
                => new SagaFlow(sagaId, _repository, _log, _errorLog);

            protected override async Task SendToChild(Dataflow<IEvent> dataflow, IEvent input)
            {
                Interlocked.Increment(ref _parallelCount);
                _log.Debug($"Saga parallel count : {_parallelCount}");
                
                await ((SagaFlow)dataflow).ProcessAsync(input);
                
                Interlocked.Decrement(ref _parallelCount);
            }
        }
        
        private class SagaFlow : Dataflow<IEvent>
        {
            private readonly IEsRepository<ISaga> _repository;
            private readonly ILog _log;
            private readonly IErrorLog _errorLog;

            private readonly string _id;
            private readonly ActionBlock<IEvent> _inputBlock;
            private TaskCompletionSource<bool> _next = new TaskCompletionSource<bool>();
            
            public SagaFlow(string id, IEsRepository<ISaga> repository, ILog log, IErrorLog errorLog)
                : base(DataflowOptions.Default)
            {
                _repository = repository;
                _id = id;
                _log = log;
                _errorLog = errorLog;

                _inputBlock = new ActionBlock<IEvent>(async e =>
                {
                    await Handle(e);
                    _next.SetResult(true);
                    _next = new TaskCompletionSource<bool>();
                });
                
                RegisterChild(_inputBlock);
            }
            
            public override ITargetBlock<IEvent> InputBlock => _inputBlock;

            /// <summary>
            /// Processes a single event asynchronously by the dataflow 
            /// </summary>
            /// <param name="e">Event to send to saga</param>
            /// <returns>Task indicating whether the event was processed by the dataflow</returns>
            public async Task<bool> ProcessAsync(IEvent e)
            {
                if (!await _inputBlock.SendAsync(e))
                    return false; 
                return await _next.Task;
            }

            private async Task Handle(IEvent e)
            {
                _log.Trace($"{typeof(TSaga).Name}::When({e.EventType})");
                
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
        }
    }
}