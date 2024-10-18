using NodaTime;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public interface IRetroactiveCommand : ICommand { }

    /// <inheritdoc cref="ZES.Infrastructure.Domain.Command" />
    public class RetroactiveCommand<TCommand> : Command, IRetroactiveCommand
        where TCommand : Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RetroactiveCommand{TCommand}"/> class.
        /// </summary>
        /// <param name="command">Underlying command</param>
        /// <param name="timestamp">Time at which the command will be actioned</param>
        public RetroactiveCommand(TCommand command, Time timestamp)
        {
            command.RetroactiveId = MessageId;
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