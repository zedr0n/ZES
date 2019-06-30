using HotChocolate.Subscriptions;

namespace ZES.GraphQL
{
    /// <summary>
    /// Base subscription type 
    /// </summary>
    public class BaseSubscription
    {
        /// <summary>
        /// GraphQL log subscription
        /// </summary>
        /// <param name="message">Message event</param>
        /// <returns>Log contents</returns>
        public LogMessage Log(IEventMessage message)
        {
            return (LogMessage)message.Payload;
        }
    }
}