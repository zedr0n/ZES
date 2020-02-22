using System;
using System.Reflection;
using ZES.Infrastructure.Attributes;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="target">Target aggregate identifier</param>
        protected Command(string target)
            : this()
        {
            Target = target;
        }

        /// <inheritdoc />
        public string Target { get; set; }

        /// <inheritdoc />
        public string RootType { get; set; } = "commands";

        /// <inheritdoc />
        public bool UseTimestamp { get; private set; } = false;

        /// <summary>
        /// Force timestamp on aggregate events
        /// </summary>
        public void ForceTimestamp()
        {
            UseTimestamp = true;
        }
    }
}