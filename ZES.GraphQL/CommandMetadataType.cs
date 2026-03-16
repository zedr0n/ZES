using System;
using HotChocolate.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors.Definitions;
using JetBrains.Annotations;
using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <inheritdoc cref="InputObjectType" />
    [UsedImplicitly]
    public class CommandMetadataType : InputObjectType<ICommandMetadata>
    {
        /// <summary>
        /// Completes the type definition during the schema creation.
        /// </summary>
        /// <param name="context">The completion context containing the current schema processing state.</param>
        /// <param name="definition">The input object type definition being completed.</param>
        protected override void OnCompleteType(ITypeCompletionContext context, InputObjectTypeDefinition definition)
        {
            definition.RuntimeType = typeof(CommandMetadata);
            base.OnCompleteType(context, definition);
        }

        /// <inheritdoc cref="InputObjectType.Configure(IInputObjectTypeDescriptor)" />
        protected override void Configure(IInputObjectTypeDescriptor<ICommandMetadata> descriptor)
        {
            descriptor.Field(t => t.MessageId).Ignore();
            descriptor.Field(t => t.Timeline);
            descriptor.Field(t => t.Timestamp).Ignore();
            base.Configure(descriptor);
        }
    }    
}

