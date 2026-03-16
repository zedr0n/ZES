using HotChocolate.Types;
using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <inheritdoc cref="InputObjectType" />
    public class QueryType<T> : InputObjectType<T>
        where T : IQuery
    {
        /// <inheritdoc cref="InputObjectType.Configure" />
        protected override void Configure(IInputObjectTypeDescriptor<T> descriptor)
        {
            descriptor.Field(t => t.Timestamp).Ignore();
            base.Configure(descriptor);
        }
    }
}