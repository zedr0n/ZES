using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Interfaces
{
    public interface ICommandLog
    {
        /// <summary>
        /// Append the command to the log
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        Task AppendCommand(ICommand command);
    }
}