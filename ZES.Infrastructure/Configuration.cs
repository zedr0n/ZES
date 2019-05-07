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
            "LogEvents",
            "GridSum",
            "InMemoryStreamStore"
        };
        
        /// <summary>
        /// Gets default timeout
        /// </summary>
        /// <value>
        /// Default timeout
        /// </value>
        public static TimeSpan Timeout => TimeSpan.FromSeconds(Debugger.IsAttached ? 600 : 1);
        
        /// <summary>
        /// Gets default number of threads per service 
        /// </summary>
        /// <value>
        /// Default number of threads per service 
        /// </value>
        public static int ThreadsPerInstance => 8;
        
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