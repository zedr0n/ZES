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
        private readonly ConnectorFlow _flow = new ConnectorFlow(new DataflowOptions { RecommendedParallelismIfMultiThreaded = Configuration.ThreadsPerInstance });
        
        /// <inheritdoc />
        public async Task<Task<string>> SubmitRequest(string url, CancellationToken token = default)
        {
            var tracked = new TrackedResult<string, string>(url, token);
            await _flow.SendAsync(tracked);
            return tracked.Task;
        }
        
        private static async Task<string> GetAsync(string url) 
        {
            using (var w = new HttpClient())
            {
                var json = await w.GetStringAsync(url);

                return string.IsNullOrEmpty(json) ? null : json;
            }
        }

        private class ConnectorFlow : Dataflow<TrackedResult<string, string>>
        {
            public ConnectorFlow(DataflowOptions dataflowOptions) 
                : base(dataflowOptions)
            {
                var block = new ActionBlock<TrackedResult<string, string>>(
                    async url =>
                    {
                        var r = await GetAsync(url.Value);
                        url.SetResult(r);
                    }, dataflowOptions.ToExecutionBlockOption(true));
                
                RegisterChild(block);
                InputBlock = block;
            }

            public override ITargetBlock<TrackedResult<string, string>> InputBlock { get; }
        }
    }
}