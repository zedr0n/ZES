using System.Collections.Generic;

namespace ZES.Interfaces
{
    /// <summary>
    /// Replay scenario
    /// </summary>
    public interface IScenario
    {
        /// <summary>
        /// Gets all the mutations in the scenario 
        /// </summary>
        List<IScenarioMutation> Requests { get; }
        
        /// <summary>
        /// Gets all results of the scenario
        /// </summary>
        List<IScenarioResult> Results { get; }
    }
    
    /// <summary>
    /// Scenario mutation
    /// </summary>
    public interface IScenarioMutation
    {
        /// <summary>
        /// Gets the GraphQL mutation string
        /// </summary>
        string GraphQl { get; }
        
        /// <summary>
        /// Gets the mutation submission timestamp
        /// </summary>
        long Timestamp { get; }
    }
    
    /// <summary>
    /// Scenario query result
    /// </summary>
    public interface IScenarioResult
    {
        /// <summary>
        /// Gets the GraphQL query string
        /// </summary>
        string GraphQl { get; }
        
        /// <summary>
        /// Gets the result of the GraphQL query
        /// </summary>
        string Result { get; }
    }
}