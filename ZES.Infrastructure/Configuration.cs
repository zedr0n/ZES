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
        public static int ThreadsPerInstance => 4;

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

        /// <summary>
        /// Check if graph is enabled
        /// </summary>
        /// <returns>True if graph is enabled</returns>
        public static bool GraphEnabled()
        {
            var enabled = Environment.GetEnvironmentVariable("Graph".ToUpper());
            return enabled != null && enabled == 1.ToString();
        }
        
        /// <summary>
        /// Graph constants
        /// </summary>
        public static class Graph
        {
            /// <summary>
            /// Graph db directory
            /// </summary>
            public const string SystemDir = "VelocityGraph";
            
            /// <summary>
            /// Event vertex 
            /// </summary>
            public const string EventVertex = "Event";
            
            /// <summary>
            /// Command vertex 
            /// </summary>
            public const string CommandVertex = "Command";
            
            /// <summary>
            /// Stream vertex
            /// </summary>
            public const string StreamVertex = "Stream";

            /// <summary>
            /// Stream metadata vertex 
            /// </summary>
            public const string StreamMetadataVertex = "StreamMetaData";

            /// <summary>
            /// Parent stream property
            /// </summary>
            public const string ParentKeyProperty = "parentKey";
            
            /// <summary>
            /// Parent stream version property 
            /// </summary>
            public const string ParentVersionProperty = "parentVersion";
            
            /// <summary>
            /// Stream key property
            /// </summary>
            public const string StreamKeyProperty = "stream";

            /// <summary>
            /// Parent stream key property
            /// </summary>
            public const string ParentStreamProperty = "parentStream";

            /// <summary>
            /// MessageId property 
            /// </summary>
            public const string MessageIdProperty = "messageId";

            /// <summary>
            /// Ancestor id property
            /// </summary>
            public const string AncestorIdProperty = "ancestorId";
            
            /// <summary>
            /// Merkle hash property
            /// </summary>
            public const string MerkleHashProperty = "merkleHash";
            
            /// <summary>
            /// Version property
            /// </summary>
            public const string VersionProperty = "version";

            /// <summary>
            /// Metadata edge
            /// </summary>
            public const string MetadataEdge = "METADATA";
            
            /// <summary>
            /// Stream edge
            /// </summary>
            public const string StreamEdge = "STREAM";
            
            /// <summary>
            /// Command edge 
            /// </summary>
            public const string CommandEdge = "COMMAND";
        }
    }
}