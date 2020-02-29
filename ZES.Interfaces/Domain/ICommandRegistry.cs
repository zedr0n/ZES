namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// Command registry
    /// </summary>
    public interface ICommandRegistry
    {
        /// <summary>
        /// Get the command handler for the command
        /// </summary>
        /// <param name="command">Command instance</param>
        /// <returns>Command handler</returns>
        ICommandHandler GetHandler(ICommand command);
    }
}