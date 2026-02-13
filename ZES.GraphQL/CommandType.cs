using System.Linq;
using HotChocolate.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors.Definitions;
using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class CommandType<T> : InputObjectType<T>
        where T : ICommand 
    {
        /// <inheritdoc />
        protected override FieldCollection<InputField> OnCompleteFields(ITypeCompletionContext context, InputObjectTypeDefinition definition)
        {
            definition.Fields.SingleOrDefault(f => f.Name == "metadata")!.Ignore = true;
            definition.Fields.SingleOrDefault(f => f.Name == "staticMetadata")!.Ignore = true;

            return base.OnCompleteFields(context, definition);
        }

        /// <inheritdoc />
        protected override void Configure(IInputObjectTypeDescriptor<T> descriptor)
        {
            descriptor.Field(t => t.Target).Ignore();
            descriptor.Field(t => t.MessageType).Ignore();
            descriptor.Field(t => t.MessageId).Ignore();
            descriptor.Field(t => t.AncestorId).Ignore();
            descriptor.Field(t => t.CorrelationId).Ignore();
            descriptor.Field(t => t.LocalId).Ignore();
            descriptor.Field(t => t.OriginId).Ignore();
            descriptor.Field(t => t.Timeline).Ignore();
            descriptor.Field(t => t.Timestamp).Ignore();
            descriptor.Field(t => t.UseTimestamp).Ignore();
            descriptor.Field(t => t.StoreInLog).Ignore();
            descriptor.Field(t => t.Pure).Ignore();
            descriptor.Field(t => t.RetroactiveId).Ignore();
            base.Configure(descriptor);
        }
    }
}