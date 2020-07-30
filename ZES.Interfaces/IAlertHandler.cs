using System.Threading.Tasks;

namespace ZES.Interfaces
{
    /// <summary>
    /// Alert handler
    /// </summary>
    public interface IAlertHandler
    {
        /// <summary>
        /// Alert handler logic 
        /// </summary>
        /// <param name="alert">Alert to handle</param>
        /// <returns>Task representing the asynchronous processing of the command</returns>
        Task Handle(IAlert alert);
    }

    /// <inheritdoc />
    public interface IAlertHandler<in TAlert> : IAlertHandler
        where TAlert : IAlert
    {
        /// <summary>
        /// Alert handler logic
        /// </summary>
        /// <param name="alert">Alert to handle</param>
        /// <returns>Completes when alert is handled</returns>
        Task Handle(TAlert alert);
    }
}