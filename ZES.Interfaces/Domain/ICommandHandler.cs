using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// CQRS Command Handler
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>
        /// Command handler aggregate logic
        /// </summary>
        /// <param name="command">Command to handle</param>
        /// <returns>Task representing the asynchronous processing of the command</returns>
        Task Handle(ICommand command);

        /// <summary>
        /// Test if the handler can process the command
        /// </summary>
        /// <param name="command">Command to handle</param>
        /// <returns>True if can process</returns>
        bool CanHandle(ICommand command);
    }
    
    /// <summary>
    /// CQRS Command Handler
    /// </summary>
    /// <typeparam name="TCommand">Command type</typeparam>
    public interface ICommandHandler<in TCommand> : ICommandHandler 
        where TCommand : ICommand
    {
        /// <summary>
        /// Command handler aggregate logic
        /// </summary>
        /// <param name="iCommand">Command to handle</param>
        /// <returns>Task representing the asynchronous processing of the command</returns>
        Task Handle(TCommand iCommand);
    }

    /// <inheritdoc />
    public interface ICommandHandler<in TCommand, out TRoot> : ICommandHandler<TCommand>
        where TCommand : ICommand
        where TRoot : IAggregate
    {
    }
}