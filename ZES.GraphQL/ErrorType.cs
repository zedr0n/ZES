using HotChocolate.Types;
using JetBrains.Annotations;
using ZES.Interfaces;

namespace ZES.GraphQL
{
    /// <inheritdoc cref="ObjectType" />
    [UsedImplicitly]
    public class ErrorType : ObjectType<IError>
    {
        /// <inheritdoc cref="ObjectType.Configure(IObjectTypeDescriptor)" />
        protected override void Configure(IObjectTypeDescriptor<IError> descriptor)
        {
            descriptor.Field(t => t.ErrorType);
            descriptor.Field(t => t.Message);
            descriptor.Field(t => t.Timestamp);
            descriptor.Field(t => t.Ignore).Ignore();
            descriptor.Field(t => t.OriginatingMessage).Ignore();
            base.Configure(descriptor);
        }
    }
}
