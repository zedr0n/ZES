using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ZES.Interfaces;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class StopWatch : IStopWatch
    {
        private readonly ConcurrentDictionary<string, long> _times = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, Stopwatch> _watches = new ConcurrentDictionary<string, Stopwatch>();
        private readonly ConcurrentDictionary<string, int> _counters = new ConcurrentDictionary<string, int>();

        /// <inheritdoc />
        public Dictionary<string, long> Totals => _times.ToDictionary(pair => pair.Key, pair => pair.Value);

        /// <inheritdoc />
        public bool Enabled { get; set; } = Environment.GetEnvironmentVariable("PERF") == 1.ToString();
        
        /// <inheritdoc />
        public long Total(string descriptor)
        {
            if (_times.TryGetValue(descriptor, out var total))
                return total;
            return 0;
        }

        /// <inheritdoc />
        public void Start(string descriptor)
        {
            if (!Enabled)
                return;
            _watches.GetOrAdd(descriptor, s => Stopwatch.StartNew());
            _counters.AddOrUpdate(descriptor, 1, (s, i) => i + 1);
        }

        /// <inheritdoc />
        public void Stop(string descriptor)
        {
            if (!Enabled)
                return;
            
            var c = _counters.AddOrUpdate(descriptor, 0, (s, i) => i - 1);
            if (c > 0)
                return;
            if (!_watches.TryRemove(descriptor, out var stopwatch)) 
                return;
            stopwatch.Stop();
            _times.AddOrUpdate(
                descriptor, 
                stopwatch.ElapsedMilliseconds,
                (s, l) => l + stopwatch.ElapsedMilliseconds);
        }
    }
}