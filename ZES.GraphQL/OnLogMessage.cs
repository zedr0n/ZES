using HotChocolate.Subscriptions;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class OnLogMessage : EventMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OnLogMessage"/> class.
        /// </summary>
        /// <param name="message">Log message</param>
        public OnLogMessage(LogMessage message)
            : base(CreateDescription(), message)
        {
        }

        private static EventDescription CreateDescription() =>
            new EventDescription("log");
    }
}