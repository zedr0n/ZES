using HotChocolate.Types;
using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class CommandType<T> : InputObjectType<T>
        where T : ICommand 
    {
        /// <inheritdoc />
        protected override void Configure(IInputObjectTypeDescriptor<T> descriptor)
        {
            descriptor.Field(t => t.Timestamp).Ignore();
            descriptor.Field(t => t.MessageId).Ignore();
            descriptor.Field(t => t.AncestorId).Ignore();
            descriptor.Field(t => t.EventType).Ignore();
            descriptor.Field(t => t.UseTimestamp).Ignore();
            base.Configure(descriptor);
        }
    }
}