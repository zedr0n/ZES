using Newtonsoft.Json;
using ZES.Interfaces;
using ZES.Interfaces.Clocks;

namespace ZES.Infrastructure.Domain;

/// <inheritdoc />
public class MessageMetadata : IMessageMetadata
{
    /// <inheritdoc />
    public MessageId MessageId { get; set; }
    
    /// <inheritdoc />
    public Time Timestamp { get; set; }

    /// <inheritdoc />
    public string Timeline { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public string Json { get; set; }
}