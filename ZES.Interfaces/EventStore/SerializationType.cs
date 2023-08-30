namespace ZES.Interfaces.EventStore;

/// <summary>
/// Serialization type enum
/// </summary>
public enum SerializationType
{
    /// <summary>
    /// Deserialize payload and metadata
    /// </summary>
    PayloadAndMetadata,
    
    /// <summary>
    /// Deserialize all metadata
    /// </summary>
    FullMetadata,
    
    /// <summary>
    /// Deserialize just dynamic metadata
    /// </summary>
    Metadata,
}