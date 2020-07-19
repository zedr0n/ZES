using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SimpleInjector;
using ZES.Interfaces;

namespace ZES.GraphQL
{
    /// <summary>
    /// Replayer class
    /// </summary>
    public class Replayer
    {
        private ServiceCollection _serviceCollection;
        
        /// <summary>
        /// Wire graphQl configuration
        /// </summary>
        /// <param name="config">Domain context type</param>
        /// <param name="logger">Logger instance ( for XUnit )</param>
        public virtual void UseGraphQl(Type config, ILogger logger = null)
        {
            _serviceCollection = new ServiceCollection();
            _serviceCollection.UseGraphQl(config, logger);
        }
        
        /// <summary>
        /// Wire graphQl configuration
        /// </summary>
        /// <param name="configs">Domain contexts</param>
        /// <param name="logger">Logger instance ( for XUnit )</param>
        public virtual void UseGraphQl(IEnumerable<Type> configs, ILogger logger = null)
        {
            _serviceCollection = new ServiceCollection();
            _serviceCollection.UseGraphQl(configs, logger);
        }

        /// <summary>
        /// Replay the scenario from log file
        /// </summary>
        /// <param name="logFile">Log filename</param>
        /// <returns>Replay result</returns>
        public async Task<ReplayResult> Replay(string logFile)
        {
            var serviceProvider = _serviceCollection.BuildServiceProvider();
            var container = serviceProvider.GetService<Container>();
            
            var provider = container.GetInstance<ISchemaProvider>();
            var recordLog = container.GetInstance<IRecordLog>();

            var scenario = await recordLog.Load(logFile);
            var replayResult = await provider.Replay(scenario);

            replayResult.Result = recordLog.Validate(scenario, replayResult);
            return replayResult;
        }
    }
}