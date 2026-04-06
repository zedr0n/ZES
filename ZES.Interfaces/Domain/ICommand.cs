namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// CQRS Command definition
    /// </summary>
    public interface ICommand : IMessage<ICommandStaticMetadata, ICommandMetadata>
    {
        /// <summary>
        /// Gets or sets command id
        /// </summary>
        string Guid { get; set; }
        
        /// <summary>
        /// Gets aggregate target id
        /// </summary>
        string Target { get; }
        
        /// <inheritdoc cref="ICommandStaticMetadata.UseTimestamp"/>
        bool UseTimestamp { get; set; }
        
        /// <inheritdoc cref="ICommandStaticMetadata.StoreInLog"/>
        bool StoreInLog { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the command is pure
        /// </summary>
        bool Pure { get; set; }

        /// <summary>
        /// Gets or sets the value indicating whether the command is ephemeral
        /// </summary>
        bool Ephemeral { get; set; }
        
        /// <summary>
        /// Copy the command
        /// </summary>
        /// <returns>Command copy</returns>
        ICommand Copy();
    }
    
    /// <inheritdoc />
    public interface ICreateCommand : ICommand { }
}