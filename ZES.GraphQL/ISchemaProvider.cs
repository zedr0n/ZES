using System;
using HotChocolate;
using HotChocolate.Execution;

namespace ZES.GraphQL
{
    public interface ISchemaProvider
    {
        IQueryExecutor Generate(Type rootQuery = null, Type rootMutation = null);
    }
}