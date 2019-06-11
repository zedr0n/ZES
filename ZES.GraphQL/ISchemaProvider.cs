using System;
using System.Collections.Generic;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using ZES.Interfaces.Domain;

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
        /// <param name="rootQuery">GraphQL root query</param>
        /// <param name="rootMutation">GraphQL root mutation</param>
        /// <returns><see cref="IQueryExecutor"/></returns>
        IQueryExecutor Generate(Type rootQuery = null, Type rootMutation = null);

        /// <summary>
        /// Builds and registers the schema stitching the provided queries and mutations
        /// </summary>
        /// <param name="services">AspNet.Core service collection</param>
        /// <param name="rootQuery">Set of root queries</param>
        /// <param name="rootMutation">Set of root mutations</param>
        /// <returns><see cref="IServiceCollection"/></returns>
        IServiceCollection Register(IServiceCollection services, IEnumerable<Type> rootQuery, IEnumerable<Type> rootMutation);

        /// <summary>
        /// Generate mutation from the internal command
        /// </summary>
        /// <param name="command">Command to convert to mutation</param>
        /// <returns>Mutation query</returns>
        string GetMutation(ICommand command);
    }
}