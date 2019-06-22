using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// CQRS Command Handler
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    public interface ICommandHandler<in TCommand> 
        where TCommand : ICommand
    {
        /// <summary>
        /// Command handler aggregate logic
        /// </summary>
        /// <param name="command">Command to handle</param>
        /// <returns>Task representing the asynchronous processing of the command</returns>
        Task Handle(TCommand command);
    }

    /// <inheritdoc />
    public interface ICommandHandler<in TCommand, out TRoot> : ICommandHandler<TCommand>
        where TCommand : ICommand
        where TRoot : IAggregate
    {
    }
}