using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotChocolate.Execution;
using ZES.Interfaces;

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
        /// Replay the scenario
        /// </summary>
        /// <param name="scenario">Scenario instance</param>
        /// <returns>Replay result</returns>
        Task<ReplayResult> Replay(IScenario scenario);
    }
}