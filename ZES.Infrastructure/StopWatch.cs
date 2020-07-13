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

        /// <inheritdoc />
        public Dictionary<string, long> Totals => _times.ToDictionary(pair => pair.Key, pair => pair.Value);

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
            var t = _watches.GetOrAdd(descriptor, s => Stopwatch.StartNew());
        }

        /// <inheritdoc />
        public void Stop(string descriptor)
        {
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