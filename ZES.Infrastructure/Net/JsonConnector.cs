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
        
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConnector"/> class.
        /// </summary>
        /// <param name="log">Log service</param>
        public JsonConnector(ILog log)
        {
            _log = log;
        }
        
        /// <inheritdoc />
        public async Task<Task<string>> SubmitRequest(string url, CancellationToken token = default)
        {
            var tracked = new TrackedResult<string, string>(url, token);
            var flow = new ConnectorFlow(Configuration.DataflowOptions, this);
            try
            {
                await flow.SendAsync(tracked);
                await flow.CompletionTask;
            }
            catch (Exception e)
            {
                _log.Errors.Add(e);
                tracked.SetResult(null);
            }
            
            return tracked.Task;
        }

        /// <inheritdoc />
        public async Task<bool> SetAsync(string url, string value)
        {
            var res = await _jsonData.GetOrAdd(url, new AsyncLazy<string>(() => value));
            return res == value;
        }

        private async Task<string> GetAsync(string url, string apiKey = "") 
        {
            using (var w = new HttpClient())
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(url),
                    Method = HttpMethod.Get,
                };
                request.Headers.Add("apikey", apiKey);
                var task = _jsonData.GetOrAdd(url, s => new AsyncLazy<string>(() =>
                {
                    _log.Info($"Initiating json connection to {url}");
                    return w.SendAsync(request).Timeout(timeout: Configuration.NetworkTimeout, message: $"Connection to {url} timed out...").ContinueWith(x => x.Result.Content.ReadAsStringAsync()).Unwrap();
                    // return w.GetStringAsync(url);
                }));
                var json = await task;
                //_log.Info(json);
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
                        var uri = url.Value;
                        var tokens = url.Value.Split(';');
                        var apiKey = string.Empty;
                        if (tokens.Length > 1)
                        {
                            uri = tokens[0];
                            apiKey = tokens[1];
                        }

                        var r = await connector.GetAsync(uri, apiKey);
                        url.SetResult(r);
                        Complete();
                    }, dataflowOptions.ToDataflowBlockOptions(true)); // .ToExecutionBlockOption(true));
                
                RegisterChild(block);
                InputBlock = block;
            }

            public override void Fault(Exception exception)
            {
                base.Fault(exception);
            }

            public override ITargetBlock<TrackedResult<string, string>> InputBlock { get; }
        }
    }
}