namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// CQRS Command definition
    /// </summary>
    public interface ICommand : IMessageEx<ICommandStaticMetadata, ICommandMetadata>
    {
        /// <summary>
        /// Gets aggregate target id
        /// </summary>
        string Target { get; }
        
        /// <inheritdoc cref="ICommandStaticMetadata.UseTimestamp"/>
        bool UseTimestamp { get; set; }
        
        /// <inheritdoc cref="ICommandStaticMetadata.StoreInLog"/>
        bool StoreInLog { get; set; }

        /// <inheritdoc cref="ICommandStaticMetadata.Pure"/>
        bool Pure { get; set; }

        /// <summary>
        /// Copy the command
        /// </summary>
        /// <returns>Command copy</returns>
        ICommand Copy();
    }
    
    /// <inheritdoc />
    public interface ICreateCommand : ICommand { }
}