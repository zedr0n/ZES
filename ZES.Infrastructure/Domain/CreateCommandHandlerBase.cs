using System;
using System.Linq;
using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class CreateCommandHandlerBase<TCommand, TRoot> : ICommandHandler<TCommand, TRoot> 
        where TRoot : class, IAggregate, new()
        where TCommand : Command, ICreateCommand
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
            
            var events = root.GetUncommittedEvents().OfType<Event>().ToList(); 
            var eventType = events.Select(e => e.MessageType).SingleOrDefault();
            if (eventType == null)
                throw new InvalidOperationException("More than one event type produced by a command");
            command.EventType = eventType;
            
            foreach (var e in events)
            {
                e.CommandId = command.MessageId;
                e.AncestorId = command.MessageId;
                e.CorrelationId = command.CorrelationId;
            }
            
            if (command.UseTimestamp)
                root.TimestampEvents(command.Timestamp);

            await _repository.Save(root);
        }

        /// <inheritdoc />
        public async Task Handle(ICommand command)
        {
            await Handle((TCommand)command);
        }

        /// <inheritdoc />
        public bool CanHandle(ICommand command) => command is TCommand;

        /// <summary>
        /// Initialisation of the new aggregate root
        /// </summary>
        /// <param name="command">Create command</param>
        /// <returns>New root instance</returns>
        protected abstract TRoot Create(TCommand command);
    }
}