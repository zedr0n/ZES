using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Infrastructure;
using ZES.Interfaces.Net;
using ZES.Interfaces.Recording;

namespace ZES.Infrastructure.Net
{
    /// <inheritdoc />
    public class JsonConnector : IJSonConnector
    {
        private readonly ConcurrentDictionary<string, HttpClient> _httpClients = new();
        private readonly ConcurrentDictionary<string, AsyncLazy<string>> _jsonData = new();
        private readonly ILog _log;
        private readonly IRecordLog _recordLog;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConnector"/> class.
        /// </summary>
        /// <param name="log">Log service</param>
        /// <param name="recordLog">Record log service</param>
        public JsonConnector(ILog log, IRecordLog recordLog)
        {
            _log = log;
            _recordLog = recordLog;
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
            var key = GetCacheKey(url);

            var res = await _jsonData.GetOrAdd(key, new AsyncLazy<string>(() => value));
            return res == value;
        }

        private AsyncLazy<string> GetAsyncImpl(HttpClient w, HttpRequestMessage request, string url) =>
            new(() =>
            {
                var logUrl = GetCacheKey(url);
                _log.Info($"Initiating json connection to {logUrl}");
                return w.SendAsync(request)
                    .Timeout(timeout: Configuration.NetworkTimeout, message: $"Connection to {logUrl} timed out...")
                    .ContinueWith(x => x.Result.Content.ReadAsStringAsync()).Unwrap();
            });
        
        private async Task<string> GetAsync(string url, string apiKey = "", bool cache = true) 
        {
            var uri = new Uri(url);
            var cacheKey = GetCacheKey(url);   // sanitized URL, no secrets
            
            var request = new HttpRequestMessage()
            {
                RequestUri = uri,
                Method = HttpMethod.Get,
            };
            var httpClient = _httpClients.GetOrAdd(uri.Host, s => new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                EnableMultipleHttp2Connections = true
            }));
            request.Headers.Add("apikey", apiKey);
           
            // 1. Recorded/test fixture values always win, even for nocache.
            if (_jsonData.TryGetValue(cacheKey, out var existing))
                return await existing;
            
            var task = cache
                ? _jsonData.GetOrAdd(cacheKey, _ => GetAsyncImpl(httpClient, request, url))
                : GetAsyncImpl(httpClient, request, url);            
            
            var json = await task;
            if (string.IsNullOrEmpty(json))
                return null;

            _recordLog.AddConnectorResult(cacheKey, json);
            return json;
        }

        private static string GetCacheKey(string url)
        {
            var tokens = url.Split(';');
            if (!Uri.TryCreate(tokens[0], UriKind.Absolute, out var uri))
                return tokens[0];
            
            var query = string.Join(
                "&",
                uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => !IsSecretKey(GetQueryKey(p))));

            return new UriBuilder(uri)
            {
                Query = query
            }.Uri.ToString();
        }

        private static string GetQueryKey(string parameter)
        {
            var separatorIndex = parameter.IndexOf('=');
            var key = separatorIndex < 0 ? parameter : parameter[..separatorIndex];
            return Uri.UnescapeDataString(key);
        }

        private static bool IsSecretKey(string key) =>
            key != null &&
            (key.Equals("api_token", StringComparison.OrdinalIgnoreCase) ||
             key.Equals("apikey", StringComparison.OrdinalIgnoreCase) ||
             key.Equals("token", StringComparison.OrdinalIgnoreCase));        
        
        private class ConnectorFlow : Dataflow<TrackedResult<string, string>>
        {
            public ConnectorFlow(DataflowOptions dataflowOptions, JsonConnector connector) 
                : base(dataflowOptions)
            {
                var block = new ActionBlock<TrackedResult<string, string>>(
                    async url =>
                    {
                        var tokens = url.Value.Split(';');
                        var uri = tokens[0];
                        var apiKey = string.Empty;
                        var cache = true;

                        for (var i = 1; i < tokens.Length; i++)
                        {
                            if (tokens[i] == "nocache")
                                cache = false;
                            else if (!string.IsNullOrEmpty(tokens[i]))
                                apiKey = tokens[i];
                        }
                        
                        var r = await connector.GetAsync(uri, apiKey, cache);
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
