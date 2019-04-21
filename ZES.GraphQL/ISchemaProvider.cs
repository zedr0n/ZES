using System;
using System.Collections.Generic;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace ZES.GraphQL
{
    public interface ISchemaProvider
    {
        IQueryExecutor Generate(Type rootQuery = null, Type rootMutation = null);
        IServiceCollection Register(IServiceCollection services, Type rootQuery, Type rootMutation);
        IServiceCollection Register(IServiceCollection services, IEnumerable<Type> rootQuery, IEnumerable<Type> rootMutation);
    }
}