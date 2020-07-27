namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class RetroactiveCommand<TCommand> : Command
        where TCommand : Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RetroactiveCommand{TCommand}"/> class.
        /// </summary>
        /// <param name="command">Underlying command</param>
        /// <param name="timestamp">Time at which the command will be actioned</param>
        public RetroactiveCommand(TCommand command, long timestamp)
        {
            command.AncestorId = MessageId;
            Target = command.Target;
            Command = command;
            Timestamp = timestamp;
        }
        
        /// <summary>
        /// Gets underlying command 
        /// </summary>
        public TCommand Command { get; }
    }
}