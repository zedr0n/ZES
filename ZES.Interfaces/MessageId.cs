using System;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces
{
    /// <summary>
    /// Message id
    /// </summary>
    public sealed record MessageId(string MessageType, Guid Id)
    {
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
            return new MessageId(MessageType: tokens[0], Id: Guid.Parse(tokens[1]));
        }
        
        /// <inheritdoc />
        public override string ToString() => $"{MessageType}:{Id}";
    }    
}
