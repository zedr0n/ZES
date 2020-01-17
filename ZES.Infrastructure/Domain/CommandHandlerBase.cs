using System;
using System.Reflection;
using System.Threading.Tasks;
using ZES.Infrastructure.Attributes;
using ZES.Infrastructure.Utils;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class CommandHandlerBase<TCommand, TRoot> : ICommandHandler<TCommand, TRoot> 
        where TRoot : class, IAggregate, new()
        where TCommand : Command
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
        public async Task<bool> IsRetroactive(TCommand iCommand)
        {
            var root = await _repository.Find<TRoot>(iCommand.Target);
            return iCommand.Timestamp < root.Timestamp;
        }
        
        /// <inheritdoc />
        public async Task Handle(TCommand command)
        {
            var root = await _repository.Find<TRoot>(command.Target);
            if (command.Timestamp < root.Timestamp)
            {
                throw new InvalidOperationException(
                    $"{typeof(TCommand).Name} command ({command.Timestamp.ToDateString()}) updating the past of the aggregate {typeof(TRoot).Name}:{command.Target} ({root.Timestamp.ToDateString()}) ");
            }

            if (root == null)
                throw new ArgumentNullException(); 
            
            Act(root, command);
            
            if (command.UseTimestamp)
                root.TimestampEvents(command.Timestamp);
            
            if (command.GetType().GetCustomAttribute<IdempotentAttribute>() != null)
                root.MakeIdempotent();
            
            await _repository.Save(root, command.MessageId);
        }
        
        /// <inheritdoc />
        public async Task Handle(ICommand command)
        {
            await Handle((TCommand)command);
        }

        /// <summary>
        /// Command action on the aggregate 
        /// </summary>
        /// <param name="root">Aggregate root</param>
        /// <param name="command">CQRS command</param>
        protected abstract void Act(TRoot root, TCommand command);
    }
}