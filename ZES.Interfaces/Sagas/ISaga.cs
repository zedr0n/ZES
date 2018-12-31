using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Sagas
{
    public interface ISaga : IEventSourced
    {
        /// <summary>
        /// Commands not yet committed 
        /// </summary>
        ICommand[] GetUncommittedCommands();

        /// <summary>
        /// Add the commands to the dispatch queue
        /// </summary>
        void SendCommand(ICommand command);
    }
}