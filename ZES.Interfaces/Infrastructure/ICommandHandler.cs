using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    public interface ICommandHandler<in TCommand> where TCommand : ICommand
    {
        /// <summary>
        /// Command handler aggregate logic
        /// </summary>
        /// <param name="command"></param>
        Task Handle(TCommand command);
    }
}