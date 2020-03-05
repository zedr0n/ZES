using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class RecordLog : IRecordLog
    {
        private readonly ITimeline _timeline;
        private readonly Lazy<string> _logFile;
        private readonly Lazy<Scenario> _scenario;
        private readonly ILog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordLog"/> class.
        /// </summary>
        /// <param name="timeline">Timeline</param>
        /// <param name="log">Application log</param>
        public RecordLog(ITimeline timeline, ILog log)
        {
            _timeline = timeline;
            _log = log;

            _logFile = new Lazy<string>(() => $"ZES_{_timeline.Now.ToDateString("yyyyMMddHHmmss")}.json");
            _scenario = new Lazy<Scenario>();
        }

        /// <inheritdoc />
        public void AddMutation(string mutation)
        {
            _scenario.Value.AddMutation(mutation, _timeline.Now);
        }

        /// <inheritdoc />
        public void AddQuery(string query, string result)
        {
            _scenario.Value.AddResult(query, result);
        }

        /// <inheritdoc />
        public async Task Flush(string logFile = null)
        {
            var s = JsonConvert.SerializeObject(_scenario.Value, Formatting.Indented);
            using (var outputFile = new StreamWriter(logFile ?? _logFile.Value))
            {
                await outputFile.WriteLineAsync(s);
            }
        }

        /// <inheritdoc />
        public bool Validate(IScenario scenario, ReplayResult result = null)
        {
            // var otherJson = JsonConvert.SerializeObject(scenario.Results, Formatting.Indented);
            // var thisJson = JsonConvert.SerializeObject(_scenario.Value.Results, Formatting.Indented);
            if (result != null)
                result.Output = JsonConvert.SerializeObject(scenario.Results.Select(r => r.Result));

            var thisResults = scenario.Results.Select(r => JToken.Parse(r.Result)).ToList();
            var otherResults = _scenario.Value.Results.Select(r => JToken.Parse(r.Result)).ToList();
            if (thisResults.Count != otherResults.Count)
            {
                _log.Warn($"Output has {thisResults.Count} results, expected {otherResults.Count}");
                return false;
            }

            var jdp = new JsonDiffPatch();
            for (var i = 0; i < thisResults.Count; ++i)
            {
                var diff = jdp.Diff(thisResults[i], otherResults[i]);
                if (diff == null) 
                    continue;
                
                _log.Warn($"Mismatch for \n {scenario.Results[i].GraphQl} : \n {diff.ToString()}");
                return false;
            }
            
            return true;
        }

        /// <inheritdoc />
        public async Task<IScenario> Load(string logFile)
        {
            string s;
            using (var inputFile = new StreamReader(logFile))
            {
                s = await inputFile.ReadToEndAsync();
            }

            var scenario = JsonConvert.DeserializeObject<Scenario>(s);
            return scenario;
        }

        /// <inheritdoc />
        public class Scenario : IScenario
        {
            /// <summary>
            /// Gets or sets the list of all mutations
            /// </summary>
            public List<Mutation> Requests { get; set; } = new List<Mutation>();
            
            /// <summary>
            /// Gets or sets all results of the scenario
            /// </summary>
            public List<ScenarioResult> Results { get; set; } = new List<ScenarioResult>();

            List<IScenarioMutation> IScenario.Requests => Requests.OfType<IScenarioMutation>().ToList();
            List<IScenarioResult> IScenario.Results => Results.OfType<IScenarioResult>().ToList();

            /// <summary>
            /// Add the mutation to scenario
            /// </summary>
            /// <param name="mutation">Mutation to add</param>
            /// <param name="timestamp">Mutation time</param>
            public void AddMutation(string mutation, long timestamp)
            {
                Requests.Add(new Mutation(mutation, timestamp));
            }

            /// <summary>
            /// Add result to scenario
            /// </summary>
            /// <param name="query">GraphQl query</param>
            /// <param name="result">GraphQl result</param>
            public void AddResult(string query, string result)
            {
                Results.Add(new ScenarioResult(query, result));
            }

            /// <inheritdoc />
            public class Mutation : IScenarioMutation
            {
                /// <summary>
                /// Initializes a new instance of the <see cref="Mutation"/> class.
                /// </summary>
                /// <param name="graphQl">GraphQl string</param>
                /// <param name="timestamp">Mutation timestamp</param>
                public Mutation(string graphQl, long timestamp)
                {
                    GraphQl = graphQl;
                    Timestamp = timestamp;
                }

                /// <inheritdoc />
                public string GraphQl { get; }

                /// <inheritdoc />
                public long Timestamp { get; }
            }

            /// <inheritdoc />
            public class ScenarioResult : IScenarioResult
            {
                /// <summary>
                /// Initializes a new instance of the <see cref="ScenarioResult"/> class.
                /// </summary>
                /// <param name="graphQl">GraphQl string</param>
                /// <param name="result">GraphQl result</param>
                public ScenarioResult(string graphQl, string result)
                {
                    GraphQl = graphQl;
                    Result = result;
                }

                /// <inheritdoc />
                public string GraphQl { get; }

                /// <inheritdoc />
                public string Result { get; }

                /// <inheritdoc />
                public bool Equal(IScenarioResult other)
                {
                    return Result == other.Result;
                }
            }
        }
    }
}