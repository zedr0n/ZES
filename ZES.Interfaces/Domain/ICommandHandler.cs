using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    public interface ICommandHandler { }
    public interface ICommandHandler<in TCommand> : ICommandHandler
        where TCommand : ICommand
    {
        /// <summary>
        /// Command handler aggregate logic
        /// </summary>
        /// <param name="command">Command to handle</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation</returns>
        Task Handle(TCommand command);
    }
}