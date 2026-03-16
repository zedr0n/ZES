using System;
using HotChocolate.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors.Definitions;
using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <inheritdoc cref="InputObjectType" />
    public class CommandStaticMetadataType : InputObjectType<ICommandStaticMetadata>
    {
        /// <summary>
        /// Completes the configuration of the <see cref="InputObjectType{T}"/> by finalizing its definition and runtime behavior.
        /// </summary>
        /// <param name="context">The context for type completion, providing access to schema-level metadata and configuration.</param>
        /// <param name="definition">The definition of the input object type being completed, containing its fields, runtime type, and other metadata.</param>
        protected override void OnCompleteType(ITypeCompletionContext context, InputObjectTypeDefinition definition)
        {
            definition.RuntimeType = typeof(CommandStaticMetadata);
            base.OnCompleteType(context, definition);
        }

        /// <inheritdoc cref="InputObjectType.Configure(IInputObjectTypeDescriptor)" />
        protected override void Configure(IInputObjectTypeDescriptor<ICommandStaticMetadata> descriptor)
        {
            descriptor.Field(t => t.StoreInLog);
            descriptor.Field(t => t.AncestorId).Ignore();
            descriptor.Field(t => t.UseTimestamp).Ignore();
            base.Configure(descriptor);
        }
    }    
}

