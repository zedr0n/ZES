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
            descriptor.Field(t => t.Position).Ignore();
            descriptor.Field(t => t.AncestorId).Ignore();
            base.Configure(descriptor);
        }
    }
}