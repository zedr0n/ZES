using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ZES.Infrastructure.EventStore;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Infrastructure;

namespace ZES.Infrastructure.Projections
{
    /// <summary>
    /// Lightweight per-key serializing dispatcher that replaces ProjectionDispatcher + ProjectionFlow.
    /// One ActionBlock at the top level; SemaphoreSlim(1) per stream key provides per-key serialization
    /// without creating a child ActionBlock per stream.
    /// </summary>
    internal class StreamDispatcher<TState> where TState : new()
    {
        private readonly ProjectionBase<TState> _projection;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ILog _log;
        private readonly CancellationToken _token;
        private readonly ConcurrentDictionary<string, int> _versions;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly ActionBlock<Tracked<IStream>> _block;

        public ITargetBlock<Tracked<IStream>> InputBlock => _block;
        public Task CompletionTask => _block.Completion;

        public StreamDispatcher(ProjectionBase<TState> projection)
        {
            _projection = projection;
            _eventStore = projection.EventStore;
            _log = projection.Log;
            _token = projection.CancellationToken;
            _versions = projection.Versions;

            _block = new ActionBlock<Tracked<IStream>>(
                DispatchAsync,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded,
                    MaxMessagesPerTask = -1,
                });

            _block.Completion.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _log?.Errors.Add(t.Exception);
            });
        }

        public async Task SignalAndWaitForCompletionAsync()
        {
            _block.Complete();
            await _block.Completion;
        }

        private async Task DispatchAsync(Tracked<IStream> tracked)
        {
            if (tracked == null) return;

            var key = tracked.Value.Key;
            if (key == null)
            {
                tracked.Complete();
                return;
            }

            var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            var acquired = false;
            try
            {
                await sem.WaitAsync(_token);
                acquired = true;
                await ProcessStream(tracked);
            }
            catch (OperationCanceledException)
            {
                tracked.Complete();
            }
            catch (Exception e)
            {
                _log?.Errors.Add(e);
                tracked.Complete();
            }
            finally
            {
                if (acquired) sem.Release();
            }
        }

        private async Task ProcessStream(Tracked<IStream> trackedStream)
        {
            _log?.Debug($"Processing {trackedStream.Value.Key}@{trackedStream.Value.Version}");
            var s = trackedStream.Value;

            if (s.Version == ExpectedVersion.NoStream)
            {
                trackedStream.Complete();
                return;
            }

            if (trackedStream.Completed.IsCompleted)
                return;

            var version = _versions.GetOrAdd(s.Key, ExpectedVersion.EmptyStream);
            var origVersion = version;

            var snapshotVersion = s.SnapshotVersion;
            if (version < snapshotVersion && _projection.Latest > s.SnapshotTimestamp)
                version = snapshotVersion - 1;

            if (s.Version <= ExpectedVersion.EmptyStream)
            {
                s = s.Copy();
                s.Version = 0;
            }

            if (version > s.Version)
            {
                _log?.Warn($"Stream {s.Key} update is version {s.Version}, behind projection version {version}");
                version = ExpectedVersion.EmptyStream;
            }

            var hasEphemeral = _eventStore.HasEphemepheralEvents(s);
            var ephemeralCount = 0;

            if (version < s.Version || hasEphemeral)
            {
                System.Collections.Generic.IList<Task> t;

                if (hasEphemeral)
                {
                    var events = await _eventStore.ReadStream<IEvent>(s, version + 1)
                        .TakeWhile(_ => !_token.IsCancellationRequested)
                        .ToList()
                        .Timeout(Configuration.Timeout);

                    var sortedEvents = events
                        .OrderBy(e => e.Timestamp)
                        .TakeWhile(e => e.Timestamp <= _projection.Latest)
                        .ToList();
                    ephemeralCount = sortedEvents.Count(e => e!.Ephemeral);
                    t = sortedEvents.Select(e => _projection.When(e)).ToList();
                }
                else
                {
                    t = await _eventStore.ReadStream<IEvent>(s, version + 1)
                        .TakeWhile(e => !_token.IsCancellationRequested && e.Timestamp <= _projection.Latest)
                        .Select(e => _projection.When(e))
                        .ToList()
                        .Timeout(Configuration.Timeout)
                        .LastOrDefaultAsync();
                }

                _log?.Debug($"{s.Key}@{s.Version} <- {version}");
                version += t.Count - ephemeralCount;
                await Task.WhenAll(t);

                if (!_versions.TryUpdate(s.Key, version, origVersion))
                    _log?.Error($"Failed updating concurrent versions for {s.Key}, {_versions[s.Key]} != {origVersion}");
            }

            trackedStream.Complete();
        }
    }
}
