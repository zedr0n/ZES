using System;
using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class CommandHandlerBase<TCommand, TRoot> : ICommandHandler<TCommand, TRoot> 
        where TRoot : class, IAggregate, new()
        where TCommand : ICommand
    {
        private readonly IEsRepository<IAggregate> _repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandHandlerBase{TCommand, TRoot}"/> class.
        /// </summary>
        /// <param name="repository">ES repository</param>
        protected CommandHandlerBase(IEsRepository<IAggregate> repository)
        {
            _repository = repository;
        }
        
        /// <inheritdoc />
        public async Task Handle(TCommand iCommand)
        {
            var root = await _repository.Find<TRoot>(iCommand.Target);
            if ( root == null )
                throw new ArgumentNullException(); 
            
            Act(root, iCommand);
            
            await _repository.Save(root, iCommand.MessageId);
        }

        /// <summary>
        /// Command action on the aggregate 
        /// </summary>
        /// <param name="root">Aggregate root</param>
        /// <param name="command">CQRS command</param>
        protected abstract void Act(TRoot root, TCommand command);
    }
}