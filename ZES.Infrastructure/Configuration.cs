using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Gridsum.DataflowEx;

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
            "LogCommands",
            "GridSum",
            "InMemoryStreamStore",
            "Common",
            "Message",
        };

        /// <summary>
        /// Gets default dataflow options 
        /// </summary>
        public static DataflowOptions DataflowOptions => new DataflowOptions
        {
            RecommendedParallelismIfMultiThreaded = ThreadsPerInstance,
            BlockMonitorEnabled = false,
            FlowMonitorEnabled = false,
        };
        
        /// <summary>
        /// Gets a value indicating whether to use local MySql or Azure MSSql
        /// </summary>
        /// <remarks>SQL requires to run SET @@global.sql_mode= ''; for long name support</remarks>
        public static bool UseMySql => true;

        /// <summary>
        /// Gets MySql database connection string
        /// </summary>
        public static string MySqlConnectionString =>
            
            // Environment.GetEnvironmentVariable("MySqlConnectionString".ToUpper());
            @"Server=localhost;Port=4406;Database=zes;Uid=zes;Pwd=zes";
        
        /// <summary>
        /// Gets azure SQL database connection string
        /// </summary>
        /// <value>
        /// Azure SQL database connection string
        /// </value>
        public static string MsSqlConnectionString => Environment.GetEnvironmentVariable("ConnectionString".ToUpper());
        
        /// <summary>
        /// Gets default throttle period for subscription invalidation
        /// </summary>
        public static TimeSpan Throttle => TimeSpan.FromMilliseconds(250);
        
        /// <summary>
        /// Gets default timeout
        /// </summary>
        /// <value>
        /// Default timeout
        /// </value>
        public static TimeSpan Timeout => TimeSpan.FromSeconds(Debugger.IsAttached ? 600 : 2);

        /// <summary>
        /// Gets the batch size for stream store
        /// </summary>
        /// <value>
        /// The batch size for stream store
        /// </value>
        public static int BatchSize => 1000;
        
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
            if (!Variables.Any(v => name.ToUpper().Contains(v.ToUpper())))
                return true;

            var n = Variables.First(v => name.ToUpper().Contains(v.ToUpper()));
            var env = Environment.GetEnvironmentVariable(n.ToUpper());
            return env != null && env != 0.ToString();
        }
    }
}