using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Net;

namespace ZES.Infrastructure.Net
{
    /// <inheritdoc />
    public class JsonConnector : IJSonConnector
    {
        private readonly ConcurrentDictionary<string, AsyncLazy<string>> _jsonData =
            new ConcurrentDictionary<string, AsyncLazy<string>>(); 
        private readonly ILog _log;
        
        private ConnectorFlow _flow;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConnector"/> class.
        /// </summary>
        /// <param name="log">Log service</param>
        public JsonConnector(ILog log)
        {
            _log = log;
            _flow = new ConnectorFlow(Configuration.DataflowOptions, this);
        }
        
        /// <inheritdoc />
        public async Task<Task<string>> SubmitRequest(string url, CancellationToken token = default)
        {
            var tracked = new TrackedResult<string, string>(url, token);
            try
            {
                await _flow.SendAsync(tracked);
            }
            catch (Exception e)
            {
                _log.Errors.Add(e);
                _flow = new ConnectorFlow(Configuration.DataflowOptions, this);
                await _flow.SendAsync(tracked);
            }
            
            return tracked.Task;
        }

        /// <inheritdoc />
        public async Task<bool> SetAsync(string url, string value)
        {
            var res = await _jsonData.GetOrAdd(url, new AsyncLazy<string>(() => value));
            return res == value;
        }

        private async Task<string> GetAsync(string url) 
        {
            using (var w = new HttpClient())
            {
                var task = _jsonData.GetOrAdd(url, s => new AsyncLazy<string>(() =>
                {
                    _log.Info($"Initiating json connection to {url}");
                    return w.GetStringAsync(url);
                }));
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
                    }, dataflowOptions.ToDataflowBlockOptions(true)); // .ToExecutionBlockOption(true));
                
                RegisterChild(block);
                InputBlock = block;
            }

            public override ITargetBlock<TrackedResult<string, string>> InputBlock { get; }
        }
    }
}