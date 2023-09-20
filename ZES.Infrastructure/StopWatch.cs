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
        private readonly ConcurrentDictionary<string, long> _times = new();
        private readonly ConcurrentDictionary<string, Stopwatch> _watches = new();
        private readonly ConcurrentDictionary<string, int> _counters = new();

        /// <inheritdoc />
        public Dictionary<string, long> Totals => _times.ToDictionary(pair => pair.Key, pair => pair.Value);

        /// <inheritdoc />
        public long Total(string descriptor)
        {
            return _times.TryGetValue(descriptor, out var total) ? total : 0;
        }

        /// <inheritdoc />
        public void Start(string descriptor)
        {
            _watches.GetOrAdd(descriptor, s => Stopwatch.StartNew());
            _counters.AddOrUpdate(descriptor, 1, (s, i) => i + 1);
        }

        /// <inheritdoc />
        public void Stop(string descriptor)
        {
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

    /// <inheritdoc />
    public class NullStopWatch : IStopWatch
    {
        /// <inheritdoc />
        public Dictionary<string, long> Totals { get; } = new();

        /// <inheritdoc />
        public long Total(string descriptor) => 0;

        /// <inheritdoc />
        public void Start(string descriptor) { }

        /// <inheritdoc />
        public void Stop(string descriptor) { }
    }
}