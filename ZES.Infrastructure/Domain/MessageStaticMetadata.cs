using System;
using Newtonsoft.Json;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain;

/// <inheritdoc />
public class MessageStaticMetadata : IMessageStaticMetadata
{
    /// <inheritdoc />
    public string MessageType { get; set; }
    
    /// <inheritdoc />
    public MessageId AncestorId { get; set; }

    /// <inheritdoc />
    public string CorrelationId { get; set; }
    
    /// <inheritdoc />
    public MessageId RetroactiveId { get; set; }
    
    /// <inheritdoc />
    public EventId LocalId { get; set; }

    /// <inheritdoc />
    public EventId OriginId { get; set; }
    
    /// <inheritdoc />
    [JsonIgnore]
    public string Json { get; set; }
}