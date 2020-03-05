using System.Threading.Tasks;

namespace ZES.Interfaces
{
    /// <summary>
    /// Record log
    /// </summary>
    public interface IRecordLog
    {
        /// <summary>
        /// Record a mutation into the log
        /// </summary>
        /// <param name="mutation">Mutation to record</param>
        void AddMutation(string mutation);
        
        /// <summary>
        /// Record query with result into the log
        /// </summary>
        /// <param name="query">Query to record</param>
        /// <param name="result">Query result</param>
        void AddQuery(string query, string result);
        
        /// <summary>
        /// Persist the log to file
        /// </summary>
        /// <param name="log">Log filename</param>
        /// <returns>Completes when file is written</returns>
        Task Flush(string log = null);
        
        /// <summary>
        /// Load scenario from the log file
        /// </summary>
        /// <param name="logFile">Log filename</param>
        /// <returns>Scenario instance valid for replaying</returns>
        Task<IScenario> Load(string logFile);
        
        /// <summary>
        /// Validate the scenario by comparing with the resulting record log
        /// </summary>
        /// <param name="scenario">Scenario to validate</param>
        /// <param name="result">Result of the replay</param>
        /// <returns>True if valid</returns>
        bool Validate(IScenario scenario, ReplayResult result = null);
    }
}