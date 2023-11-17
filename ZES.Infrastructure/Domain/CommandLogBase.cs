using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ZES.Infrastructure.EventStore;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public abstract class CommandLogBase<TNewStreamMessage, TStreamMessage> : ICommandLog
    {
        private readonly ISerializer<ICommand> _serializer;
        private readonly ITimeline _timeline;
        private readonly ILog _log;

        private readonly ConcurrentDictionary<string, FailedCommandsSingleHolder> _failedCommandsSingleHolders;
        private readonly ConcurrentDictionary<string, IStream> _streams = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLogBase{TStreamMessage, TNewStreamMessage}"/> class.
        /// </summary>
        /// <param name="serializer">Serializer</param>
        /// <param name="timeline">Current timeline tracker</param>
        /// <param name="log">Application log</param>
        public CommandLogBase(ISerializer<ICommand> serializer, ITimeline timeline, ILog log)
        {
            _serializer = serializer;
            _timeline = timeline;
            _log = log;
            _failedCommandsSingleHolders = new ConcurrentDictionary<string, FailedCommandsSingleHolder>();
        }

        /// <inheritdoc />
        public IObservable<HashSet<ICommand>> FailedCommands =>
            _failedCommandsSingleHolders.GetOrAdd(_timeline.Id, s => new FailedCommandsSingleHolder()).FailedCommands();

        /// <summary>
        /// Gets the serializer instance
        /// </summary>
        protected ISerializer<ICommand> Serializer => _serializer;

        /// <summary>
        /// Gets the log instance
        /// </summary>
        protected ILog Log => _log;

        /// <inheritdoc />
        public async Task<IEnumerable<IStream>> ListStreams(string branchId)
        {
            await Task.CompletedTask;
            return _streams.Where(s => s.Value.Timeline == branchId).Select(s => s.Value);
        }

        /// <inheritdoc />
        public IObservable<ICommand> ReadStream(IStream stream, int start, int count = -1)
        {
            var obs = Observable.Create(async (IObserver<ICommand> observer) =>
                await ReadStreamStore(observer, stream, start, Configuration.BatchSize));
            return obs;
        }

        /// <inheritdoc />
        public async Task<ICommand> GetCommand(IEvent e)
        {
            var key = Key(e.AncestorId.MessageType);
            var stream = new Stream(key, ExpectedVersion.Any);
            var obs = ReadStream(stream, ExpectedVersion.EmptyStream + 1); 

            if(e.AncestorId == default)
                _log.Error($"No ancestor id found for {e.MessageId}");
            _log.Debug($"Searching for originating command in stream {key} for {e}");
            var command = await obs.FirstOrDefaultAsync(c => e.AncestorId == c.MessageId).Timeout(Configuration.Timeout);
            return command;
        }

        /// <inheritdoc />
        public async Task<ICommand> GetCommand(ICommand c)
        {
            var key = Key(c.MessageType);
            var stream = new Stream(key, ExpectedVersion.Any);
            var obs = ReadStream(stream, 0);

            var command = await obs.Where(x => x.MessageId == c.MessageId).SingleOrDefaultAsync();
            return command;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ICommand>> GetCommands(string branchId)
        {
            var streams = (await ListStreams(branchId)).ToList();
            if (streams.Count == 0)
                return new List<ICommand>();
            var obs = streams.Select(s => ReadStream(s, 0)).Aggregate((r, c) => r.Concat(c));
            return await obs.ToList();
        }

        /// <inheritdoc />
        public IStream GetStream(ICommand c, string branchId = null)
        {
            return _streams.SingleOrDefault(x => x.Key == Key(c.MessageType, branchId)).Value;
        }

        /// <inheritdoc />
        public async Task AddFailedCommand(ICommand command)
        {
            var holder = _failedCommandsSingleHolders.GetOrAdd(command.Timeline, b => new FailedCommandsSingleHolder());
            await holder.UpdateState(b =>
            {
                b.Timeline = command.Timeline;
                b.Commands.Add(command);
                return b;
            });
        }

        /// <inheritdoc />
        public async Task AppendCommand(ICommand command)
        {
            var message = Encode(command);

            var key = Key(command.MessageType);
            _log.Debug($"Adding command to stream {key}: {command}");
            var version = await AppendToStream(key, message);
            var stream = _streams.GetOrAdd(key, new Stream(key, version));

            // resolve the command if failed
            var holder = _failedCommandsSingleHolders.GetOrAdd(command.Timeline, b => new FailedCommandsSingleHolder());
            var failedCommands = await holder.FailedCommands().FirstAsync();
            if (failedCommands.Count == 0)
                return;
            
            await holder.UpdateState(b =>
            {
                b.Timeline = command.Timeline;
                b.Commands.RemoveWhere(c => c.MessageId == command.MessageId);
                return b;
            });
        }

        /// <inheritdoc />
        public abstract Task DeleteBranch(string branchId);

        /// <summary>
        /// Store implementation of stream read as observable
        /// </summary>
        /// <param name="observer">Observer instance</param>
        /// <param name="stream">Stream definition</param>
        /// <param name="position">Stream position</param>
        /// <param name="count">Number of commands to read</param>
        /// <returns>Task representing the completion of observable</returns>
        protected abstract Task ReadStreamStore(IObserver<ICommand> observer, IStream stream, int position, int count);

        /// <summary>
        /// Store implementation of stream write
        /// </summary>
        /// <param name="key">Stream key</param>
        /// <param name="message">Stream message</param>
        /// <returns>Task completes when write is completed, returning the version written</returns>
        protected abstract Task<int> AppendToStream(string key, TNewStreamMessage message);

        /// <summary>
        /// Encode the command to stream message
        /// </summary>
        /// <param name="command">Command instance</param>
        /// <returns>Command stream message</returns>
        protected abstract TNewStreamMessage Encode(ICommand command);
        
        private string Key(string commandType, string branchId = null)
        {
            if (string.IsNullOrEmpty(commandType))
                throw new InvalidOperationException("Event type not known for command");
            branchId ??= _timeline.Id;
            return $"{branchId}:Command:{commandType.Split('.').Last()}";
        }
    }
}