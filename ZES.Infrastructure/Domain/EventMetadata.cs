using System.ComponentModel;
using Newtonsoft.Json;
using ZES.Interfaces;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc cref="IEvent" />
    public class EventMetadata : Message, IEventMetadata
    {
        /// <inheritdoc />
        public int Version { get; set; }

        /// <inheritdoc />
        public string StreamHash { get; set; }

        /// <inheritdoc />
        [DefaultValue("")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ContentHash { get; set; } = string.Empty;
    }
}