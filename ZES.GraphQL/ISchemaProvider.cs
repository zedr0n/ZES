using System;
using HotChocolate;

namespace ZES.GraphQL
{
    public interface ISchemaProvider
    {
        ISchema Generate(Type rootQuery = null, Type rootMutation = null);
    }
}