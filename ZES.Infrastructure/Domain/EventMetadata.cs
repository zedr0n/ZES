using System.ComponentModel;
using Newtonsoft.Json;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Event metadata
    /// </summary>
    public class EventMetadata : MessageMetadata, IEventMetadata
    {
        /// <inheritdoc />
        public int Version { get; set; }

        /// <inheritdoc />
        public string Stream { get; set; }

        /// <inheritdoc />
        [DefaultValue("")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ContentHash { get; set; } = string.Empty;
        
        /// <inheritdoc />
        public string StreamHash { get; set; }
        
        /// <inheritdoc />
        public IEventMetadata Copy()
        {
            var copy = MemberwiseClone() as EventMetadata;
            return copy;
        }
    }
}