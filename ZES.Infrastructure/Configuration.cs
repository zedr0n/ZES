using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Interfaces.Clocks;
using Newtonsoft.Json;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Event store backend type
    /// </summary>
    public enum EventStoreBackendType
    {
        /// <summary>
        /// SQLStreamStore backend
        /// </summary>
        SqlStreamStore,
        
        /// <summary>
        /// EventStore backend
        /// </summary>
        EventStore,
        
        /// <summary>
        /// Redis Streams backend
        /// </summary>
        Redis,
    }
    
    /// <summary>
    /// Configuration override interface
    /// </summary>
    public interface IConfigurationOverride
    {
        /// <summary>
        /// Main method to apply all the configuration overrides
        /// </summary>
        void ApplyOverride();
    }    
    
    /// <summary>
    /// Composite configuration override class to combine multiple overrides
    /// </summary>
    public class CompositeConfigurationOverride(params IConfigurationOverride[] overrides) : IConfigurationOverride
    {
        private readonly IEnumerable<IConfigurationOverride> _overrides = overrides;

        /// <inheritdoc />
        public void ApplyOverride()
        {
            foreach (var configOverride in _overrides)
            {
                configOverride.ApplyOverride();
            }
        }
    }    
    
    /// <summary>
    /// Static configuration instance
    /// </summary>
    public static class Configuration
    {
        private static readonly ConcurrentDictionary<string, bool> _logEnabled = new ConcurrentDictionary<string, bool>();

        private static readonly HashSet<string> Variables = new HashSet<string>
        {
            "AddCommand",
            "LogEvents",
            "LogCommands",
            "GridSum",
            "InMemoryStreamStore",
            "Common",
            "Message",
            "DeleteBranch",
            "Clone"
        };

        static Configuration()
        {
            var env = Environment.GetEnvironmentVariable("UseTcpStore".ToUpper());
            if (env != null && 1.ToString() == env)
                EventStoreBackendType = EventStoreBackendType.EventStore;
            ReplicaName = Environment.GetEnvironmentVariable("ReplicaName".ToUpper()) ?? "None";
            if (Environment.GetEnvironmentVariable("USEVERSIONCACHE") == "0")
                UseVersionCache = false;
        }

        /// <summary>
        /// Gets a value indicating whether to use fast temporary branches by not persisting to stream store
        /// </summary>
        public static bool FastTemporaryBranches { get; } = true;

        /// <summary>
        /// Gets a value indicating whether to exclude static metadata from the stream message metadata
        /// </summary>
        public static bool StoreMetadataSeparately { get; } = true;

        /// <summary>
        /// Gets a value indicating which formatting to use
        /// </summary>
        public static Formatting JsonFormatting { get; } = Formatting.None;
        
        /// <summary>
        /// Gets a value indicating whether to use compact deserialization when doing retroactive actions
        /// </summary>
        public static bool UseCompactDeserializationForRetroactiveOperations { get; set; } = true;
        
        /// <summary>
        /// Gets a value indicating whether to use the version cache
        /// </summary>
        public static bool UseVersionCache { get; } = true;
        
        /// <summary>
        /// Gets a value indicating whether to use merge functionality during retroactive insert
        /// </summary>
        public static bool UseMergeForRetroactiveInsert { get; } = false;
        
        /// <summary>
        /// Gets a value indicating whether to use json name table during serialisation
        /// </summary>
        public static bool UseJsonNameTable { get; } = false;

        /// <summary>
        /// Gets a value indicating whether to use json array pool 
        /// </summary>
        public static bool UseJsonArrayPool { get; } = false;

        /// <summary>
        /// Gets a value indicating whether to roll back multiple consecutive events at once
        /// </summary>
        public static bool RollbackMultipleEventsAtOnce { get; } = true;

        /// <summary>
        /// Gets a value indicating whether to delete streams instead of trimming
        /// </summary>
        public static bool DeleteStreamsInsteadOfTrimming { get; } = true;

        /// <summary>
        /// Gets a value indicating whether to use legacy merge
        /// </summary>
        public static bool UseLegacyMerge => false;
        
        /// <summary>
        /// Gets a value indicating whether to use the Embedded TCP Store
        /// </summary>
        public static bool UseEmbeddedTcpStore => true;

        /// <summary>
        /// Gets the Event Store RPC connection string
        /// </summary>
        public static string RpcConnectionString => "esdb://localhost:2113?tls=false";

        /// <summary>
        /// Gets the replica name
        /// </summary>
        public static string ReplicaName { get; } 
        
        /// <summary>
        /// Gets the Event Store TCP connection string
        /// </summary>
        public static string TcpConnectionString => "ConnectTo=tcp://admin:changeit@localhost:1113";

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
        /// Gets a value indicating whether to use the limited concurrency scheduler
        /// </summary>
        public static bool UseLimitedScheduler { get; } = false;

        /// <summary>
        /// Gets the max messages per task
        /// </summary>
        public static int MaxMessagesPerTask { get; } = 1;

        /// <summary>
        /// Gets the limited thread scheduler
        /// </summary>
        public static LimitedConcurrencyLevelTaskScheduler LimitedTaskScheduler { get; } =
            new LimitedConcurrencyLevelTaskScheduler(ThreadsPerInstance);

        /// <summary>
        /// Gets a value indicating which backend to use for event store
        /// </summary>
        public static EventStoreBackendType EventStoreBackendType { get; } = EventStoreBackendType.SqlStreamStore;
        
        /// <summary>
        /// Gets a value indicating whether to use SQLStreamStore instead of Event Store
        /// </summary>
        public static bool UseSqlStore { get; private set; } = true;
        
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
        /// Gets default network timeout
        /// </summary>
        /// <value>
        /// Default timeout
        /// </value>
        public static TimeSpan NetworkTimeout => TimeSpan.FromSeconds(10);
        
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
        public static int ThreadsPerInstance => Debugger.IsAttached ? 1 : Math.Max(Environment.ProcessorCount / 2, 1);

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
            return _logEnabled.GetOrAdd(name, x =>
            {
                if (!Variables.Any(v => x.ToUpper().Contains(v.ToUpper())))
                    return true;

                var n = Variables.First(v => x.ToUpper().Contains(v.ToUpper()));
                var env = Environment.GetEnvironmentVariable(n.ToUpper());
                return env != null && env != 0.ToString();
            });
        }
    }
}