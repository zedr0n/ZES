using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc cref="ICommand" />
    public class Command : Message, ICommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        protected Command()
        {
            MessageId = Guid.NewGuid();
        }

        /// <inheritdoc />
        [JsonIgnore]
        public virtual string Target { get; set; }

        /// <inheritdoc />
        public string EventType { get; set; }

        /// <inheritdoc />
        public bool UseTimestamp { get; set; } = false;

        /// <inheritdoc />
        public bool StoreInLog { get; set; } = true;

        /// <inheritdoc />
        public bool Pure { get; set; }

        /// <inheritdoc />
        public class Comparer : IEqualityComparer<ICommand>
        {
            /// <inheritdoc />
            public bool Equals(ICommand x, ICommand y)
            {
                if (x == null || y == null)
                    return false;
                return x.MessageId.Equals(y.MessageId);
            }

            /// <inheritdoc />
            public int GetHashCode(ICommand obj)
            {
                return obj.MessageId.GetHashCode();
            }
        }
    }
}