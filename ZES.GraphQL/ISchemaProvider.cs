using System;
using System.Collections.Generic;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace ZES.GraphQL
{
    /// <summary>
    /// Schema builder for GraphQL using <see cref="HotChocolate"/>
    /// </summary>
    public interface ISchemaProvider
    {
        /// <summary>
        /// Generates the schema from specified root query and mutation 
        /// </summary>
        /// <returns><see cref="IQueryExecutor"/></returns>
        IQueryExecutor Build();

        /// <summary>
        /// Builds and registers the schema stitching the provided queries and mutations
        /// </summary>
        /// <returns><see cref="IServiceCollection"/></returns>
        IServiceCollection Services();
    }
}