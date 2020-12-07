using System;
using System.Linq;
using System.Threading.Tasks;
using Gridsum.DataflowEx;
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

        /// <summary>
        /// Gets or sets a value indicating whether hash computation is required
        /// </summary>
        protected bool ComputeHash { get; set; } = false;

        /// <inheritdoc />
        public async Task Handle(TCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(typeof(TCommand).GetFriendlyName());
            
            var root = await _repository.Find<TRoot>(command.Target, ComputeHash);
            
            if (root == null)
                throw new ArgumentNullException(typeof(TRoot).GetFriendlyName());
            if (command.Timestamp < root.Timestamp)
            {
                throw new InvalidOperationException(
                    $"{typeof(TCommand).Name} command ({command.Timestamp}) updating the past of the aggregate {typeof(TRoot).Name}:{command.Target} ({root.Timestamp}) ");
            }
            
            Act(root, command);
            
            if (command.UseTimestamp)
                root.TimestampEvents(command.Timestamp);

            var events = root.GetUncommittedEvents().OfType<Event>().ToList(); 
            var eventType = events.Select(e => e.MessageType).SingleOrDefault();
            if (eventType == null)
                throw new InvalidOperationException("More than one event type produced by a command");
            foreach (var e in events)
            {
                e.CommandId = command.MessageId;
                e.AncestorId = command.MessageId;
            }

            command.EventType = eventType;
            
            await _repository.Save(root);
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