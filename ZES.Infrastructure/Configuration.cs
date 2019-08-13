using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Static configuration instance
    /// </summary>
    public static class Configuration
    {
        public static class Graph
        {
            public const string SystemDir = "VelocityGraph";
            
            public const string VertexEventType = "Event";
            public const string VertexCommandType = "Command";
            public const string StreamVertexType = "Stream";

            public const string StreamKey = "streamKey";    
            public const string VertexMessageId = "messageId";
            public const string VertexMerkleHash = "merkleHash";
            public const string VertexVersion = "version";
        
            public const string EdgeType = "CAUSES";
            public const string EdgeStreamType = "STREAM";
            public const string EdgeCommandType = "COMMAND";
        }

        private static readonly HashSet<string> Variables = new HashSet<string>
        {
            "AddCommand",
            "LogEvents",
            "GridSum",
            "InMemoryStreamStore",
            "Common"
        };

        /// <summary>
        /// Gets azure SQL database connection string
        /// </summary>
        /// <value>
        /// Azure SQL database connection string
        /// </value>
        public static string MsSqlConnectionString => Environment.GetEnvironmentVariable("ConnectionString".ToUpper());
        
        /// <summary>
        /// Gets default timeout
        /// </summary>
        /// <value>
        /// Default timeout
        /// </value>
        public static TimeSpan Timeout => TimeSpan.FromSeconds(Debugger.IsAttached ? 600 : 1);

        /// <summary>
        /// Gets the batch size for stream store
        /// </summary>
        /// <value>
        /// The batch size for stream store
        /// </value>
        public static int BatchSize => 100;
        
        /// <summary>
        /// Gets default number of threads per service 
        /// </summary>
        /// <value>
        /// Default number of threads per service 
        /// </value>
        public static int ThreadsPerInstance => 8;

        /// <summary>
        /// Check if Common.Logging logging is allowed
        /// </summary>
        /// <returns>True if enabled</returns>
        public static bool CommonLogEnabled()
        {
            if (LogEnabled("Common") || LogEnabled("GridSum") || LogEnabled("InMemoryStreamStore"))
                return true;
            return false;
        }
        
        /// <summary>
        /// Check if log is enabled for specified category
        /// </summary>
        /// <param name="name">Log category</param>
        /// <returns>True if log is enabled for category</returns>
        public static bool LogEnabled(string name)
        {
            if (!Variables.Contains(name))
                return true;

            var env = Environment.GetEnvironmentVariable(name.ToUpper());
            return env != null && env != 0.ToString();
        }
    }
}