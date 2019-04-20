using HotChocolate;

namespace ZES.GraphQL
{
    public interface ISchemaProvider
    {
        ISchema Generate();
    }
}