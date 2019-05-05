using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ZES.Infrastructure
{
    public static class Configuration
    {
        private static readonly HashSet<string> Variables = new HashSet<string>
        {
            "LogEvents",
            "GridSum",
            "InMemoryStreamStore"
        };

        public static TimeSpan Timeout => TimeSpan.FromSeconds(Debugger.IsAttached ? 600 : 1);
        public static int ThreadsPerInstance => 8;
        
        public static bool LogEnabled(string name)
        {
            if (!Variables.Contains(name))
                return true;

            var env = Environment.GetEnvironmentVariable(name.ToUpper());
            if (env == null || env == 0.ToString())
                return false;
            
            return true;
        }
    }
}