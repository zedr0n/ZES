using System;
using HotChocolate.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors.Definitions;
using JetBrains.Annotations;
using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    [UsedImplicitly]
    public class CommandMetadataType : InputObjectType<ICommandMetadata>
    {
        /// <inheritdoc />
        protected override void OnCompleteType(ITypeCompletionContext context, InputObjectTypeDefinition definition)
        {
            definition.RuntimeType = typeof(CommandMetadata);
            base.OnCompleteType(context, definition);
        }

        /// <inheritdoc />
        protected override void Configure(IInputObjectTypeDescriptor<ICommandMetadata> descriptor)
        {
            descriptor.Field(t => t.MessageId).Ignore();
            descriptor.Field(t => t.Timeline);
            descriptor.Field(t => t.Timestamp).Ignore();
            base.Configure(descriptor);
        }
    }    
}

