using HotChocolate.Types;
using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class QueryType<T> : InputObjectType<T>
        where T : IQuery
    {
        /// <inheritdoc />
        protected override void Configure(IInputObjectTypeDescriptor<T> descriptor)
        {
            descriptor.Field(t => t.Timestamp).Ignore();
            base.Configure(descriptor);
        }
    }
}