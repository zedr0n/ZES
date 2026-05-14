using System.Collections.Generic;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces.Recording
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

        /// <summary>
        /// Gets cached JSON connector responses required to replay the scenario without network access.
        /// </summary>
        List<IConnectorResult> ConnectorResults { get; }

        /// <summary>
        /// Sort the scenario actions
        /// </summary>
        void Sort();
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
        Time Timestamp { get; }
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

        /// <summary>
        /// Check if the results are the same
        /// </summary>
        /// <param name="other">Other scenario results</param>
        /// <returns>True if result is the same</returns>
        bool Equal(IScenarioResult other);
    }

    /// <summary>
    /// Cached JSON connector response captured while recording a scenario.
    /// </summary>
    public interface IConnectorResult
    {
        /// <summary>
        /// Gets the sanitized connector cache key.
        /// </summary>
        string Url { get; }

        /// <summary>
        /// Gets the JSON response associated with the connector cache key.
        /// </summary>
        string Value { get; }
    }
}
