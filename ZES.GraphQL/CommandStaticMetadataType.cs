using System;
using HotChocolate.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors.Definitions;
using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class CommandStaticMetadataType : InputObjectType<ICommandStaticMetadata>
    {
        /// <inheritdoc />
        protected override void OnCompleteType(ITypeCompletionContext context, InputObjectTypeDefinition definition)
        {
            definition.RuntimeType = typeof(CommandStaticMetadata);
            base.OnCompleteType(context, definition);
        }

        /// <inheritdoc />
        protected override void Configure(IInputObjectTypeDescriptor<ICommandStaticMetadata> descriptor)
        {
            descriptor.Field(t => t.StoreInLog);
            descriptor.Field(t => t.AncestorId).Ignore();
            descriptor.Field(t => t.UseTimestamp).Ignore();
            descriptor.Field(t => t.Pure).Ignore();
            base.Configure(descriptor);
        }
    }    
}

