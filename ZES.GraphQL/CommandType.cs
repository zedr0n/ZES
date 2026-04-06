using System.Linq;
using HotChocolate.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors.Definitions;
using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <inheritdoc cref="InputObjectType" />
    public class CommandType<T> : InputObjectType<T>
        where T : ICommand 
    {
        /// <summary>
        /// Finalizes the fields of the input object type. This method applies any additional
        /// processing or modifications to the fields, such as ignoring fields with specific names.
        /// </summary>
        /// <param name="context">The type completion context used to complete the input type definition.</param>
        /// <param name="definition">The input object type definition containing the fields to process.</param>
        /// <returns>The completed collection of input fields after applying modifications.</returns>
        protected override FieldCollection<InputField> OnCompleteFields(ITypeCompletionContext context,
            InputObjectTypeDefinition definition)
        {
            definition.Fields.SingleOrDefault(f => f.Name == "metadata")!.Ignore = true;
            definition.Fields.SingleOrDefault(f => f.Name == "staticMetadata")!.Ignore = true;

            return base.OnCompleteFields(context, definition);
        }

        /// <inheritdoc cref="InputObjectType.Configure(IInputObjectTypeDescriptor)"/>
        protected override void Configure(IInputObjectTypeDescriptor<T> descriptor)
        {
            descriptor.Field(t => t.Target).Ignore();
            descriptor.Field(t => t.MessageType).Ignore();
            descriptor.Field(t => t.MessageId).Ignore();
            descriptor.Field(t => t.Guid).Type<StringType>().DefaultValue(null);
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
            descriptor.Field(t => t.Ephemeral).Ignore();
            base.Configure(descriptor);
        }
    }
}