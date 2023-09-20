using System;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces
{
    /// <summary>
    /// Message id
    /// </summary>
    public sealed record MessageId(string MessageType, Guid Id)
    {
        private string _string;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageId"/> class.
        /// </summary>
        /// <param name="messageType">Message type</param>
        /// <param name="id">Guid</param>
        /// <param name="messageIdString">Message id string</param>
        private MessageId(string messageType, Guid id, string messageIdString)
            : this(messageType, id)
        {
            _string = messageIdString;
        }
        
        /// <summary>
        /// Create the <see cref="MessageId"/> from string
        /// </summary>
        /// <param name="str">String representation</param>
        /// <returns><see cref="MessageId"/> instance</returns>
        public static MessageId Parse(string str)
        {
            var tokens = str.Split(':');
            if (tokens.Length != 2)
                throw new InvalidCastException($"MessageId should be of format {nameof(MessageType)}:{nameof(Id)}");
            return new MessageId(tokens[0], Guid.Parse(tokens[1]), str);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            _string ??= MessageType + ":" + Id;
            return _string;
        }
    }    
}
