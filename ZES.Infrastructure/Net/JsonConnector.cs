using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Interfaces.Net;

namespace ZES.Infrastructure.Net
{
    /// <inheritdoc />
    public class JsonConnector : IJSonConnector
    {
        private readonly ConcurrentDictionary<string, AsyncLazy<string>> _jsonData =
            new ConcurrentDictionary<string, AsyncLazy<string>>(); 
        private readonly ConnectorFlow _flow;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConnector"/> class.
        /// </summary>
        public JsonConnector()
        {
            _flow = new ConnectorFlow(new DataflowOptions { RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance }, this);
        }
        
        /// <inheritdoc />
        public async Task<Task<string>> SubmitRequest(string url, CancellationToken token = default)
        {
            var tracked = new TrackedResult<string, string>(url, token);
            await _flow.SendAsync(tracked);
            return tracked.Task;
        }
        
        private async Task<string> GetAsync(string url) 
        {
            using (var w = new HttpClient())
            {
                var task = _jsonData.GetOrAdd(url, s => new AsyncLazy<string>(() => w.GetStringAsync(url)));
                var json = await task;
                return string.IsNullOrEmpty(json) ? null : json;
            }
        }

        private class ConnectorFlow : Dataflow<TrackedResult<string, string>>
        {
            public ConnectorFlow(DataflowOptions dataflowOptions, JsonConnector connector) 
                : base(dataflowOptions)
            {
                var block = new ActionBlock<TrackedResult<string, string>>(
                    async url =>
                    {
                        var r = await connector.GetAsync(url.Value);
                        url.SetResult(r);
                    }, dataflowOptions.ToExecutionBlockOption(true));
                
                RegisterChild(block);
                InputBlock = block;
            }

            public override ITargetBlock<TrackedResult<string, string>> InputBlock { get; }
        }
    }
}