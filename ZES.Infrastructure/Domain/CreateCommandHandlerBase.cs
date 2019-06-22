using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class CreateCommandHandlerBase<TCommand, TRoot> : ICommandHandler<TCommand, TRoot> 
        where TRoot : class, IAggregate, new()
        where TCommand : ICreateCommand
    {
        private readonly IEsRepository<IAggregate> _repository;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateCommandHandlerBase{TCommand, TRoot}"/> class.
        /// </summary>
        /// <param name="repository">ES repository</param>
        protected CreateCommandHandlerBase(IEsRepository<IAggregate> repository)
        {
            _repository = repository;
        }
        
        /// <inheritdoc />
        public async Task Handle(TCommand command)
        {
            var root = Create(command);
            await _repository.Save(root, command.MessageId);
        }

        /// <summary>
        /// Initialisation of the new aggregate root
        /// </summary>
        /// <param name="command">Create command</param>
        /// <returns>New root instance</returns>
        protected abstract TRoot Create(TCommand command);
    }
}