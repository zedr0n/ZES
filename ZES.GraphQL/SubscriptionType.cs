using HotChocolate.Types;

namespace ZES.GraphQL
{
    /// <summary>
    /// Log subscription type
    /// </summary>
    public class SubscriptionType : ObjectType<BaseSubscription>
    {
        /// <inheritdoc />
        protected override void Configure(IObjectTypeDescriptor<BaseSubscription> descriptor)
        {
            // descriptor.Field(t => t.Log(default))
            //    .Type<NonNullType<ObjectType<LogMessage>>>();
        }
    }
}