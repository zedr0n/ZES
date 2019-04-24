using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<string, SagaFlow> _flows = new ConcurrentDictionary<string, SagaFlow>();
        
        /// <summary>
        /// Initializes a new instance of the <see cref="SagaHandler{TSaga}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="repository">Saga repository</param>
        /// <param name="log">Application log</param>
        public SagaHandler(IMessageQueue messageQueue, IEsRepository<ISaga> repository, ILog log)
        {
            var sagaBlock = new ActionBlock<IEvent>(
                async e =>
            {
                var sagaId = new TSaga().SagaId(e);

                var flow = _flows.GetOrAdd(sagaId, new SagaFlow(repository, sagaId, log));
                await flow.InputBlock.SendAsync(e); 
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 8 }); 

            messageQueue.Messages.Subscribe(async e => await sagaBlock.SendAsync(e));
        }
        
        private class SagaFlow : Dataflow<IEvent>
        {
            private readonly IEsRepository<ISaga> _repository;
            private readonly ILog _log;

            private readonly string _id;
            private readonly ActionBlock<IEvent> _inputBlock;
            
            public SagaFlow(IEsRepository<ISaga> repository, string id, ILog log)
                : base(DataflowOptions.Default)
            {
                _repository = repository;
                _id = id;
                _log = log;
                _inputBlock = new ActionBlock<IEvent>(async e => await Handle(e));
                
                RegisterChild(_inputBlock);
            }
            
            public override ITargetBlock<IEvent> InputBlock => _inputBlock;

            private async Task Handle(IEvent e)
            {
                var saga = await _repository.GetOrAdd<TSaga>(_id);  
                _log.Trace($"{saga.GetType().Name}::When({e.EventType})");
                saga.When(e);
                await _repository.Save(saga);  
            }
        }
    }
}