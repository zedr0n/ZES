using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class Command : ICommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        protected Command() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="target">Target aggregate identifier</param>
        protected Command(string target)
        {
            Target = target;
        }

        /// <inheritdoc />
        public string Target { get; set; }

        /// <inheritdoc />
        public long? Timestamp { get; set; }
    }
}