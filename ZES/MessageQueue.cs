using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using SimpleInjector;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;
using ZES.Utils;

namespace ZES
{
    /// <inheritdoc />
    public class MessageQueue : IMessageQueue
    {
        private readonly ILog _log;
        private readonly ITimeline _timeline;
        private readonly Subject<IEvent> _messages = new();
        private readonly Subject<IAlert> _alerts = new();
        private readonly ConcurrentDictionary<string, UncompletedMessagesSingleHolder> _messagesHolderDict;
        private readonly ConcurrentDictionary<Guid, CommandStateHolder> _commandStateHolders;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageQueue"/> class.
        /// </summary>
        /// <param name="log">Application log</param>
        /// <param name="timeline">Timeline</param>
        public MessageQueue(ILog log, ITimeline timeline)
        {
            _log = log;
            _timeline = timeline;
            _messagesHolderDict = new ConcurrentDictionary<string, UncompletedMessagesSingleHolder>();
            _commandStateHolders = new ConcurrentDictionary<Guid, CommandStateHolder>();
            RetroactiveExecution.DistinctUntilChanged()
                .Throttle(x => Observable.Timer(TimeSpan.FromMilliseconds(x ? 0 : 10)))
                .StartWith(false)
                .DistinctUntilChanged()
                .Subscribe(x => log.Info($"Retroactive execution : {x}"));
            
            // var combinedStates = _commandStateHolders.Select(x => x.Value.CommandState().Select(s => new { x.Key, s }) ).Merge();

        }
        
        /// <inheritdoc />
        public IObservable<int> UncompletedMessages =>
            _messagesHolderDict.GetOrAdd(_timeline.Id, s => new UncompletedMessagesSingleHolder())
                .UncompletedMessages();

        /// <inheritdoc />
        public IObservable<CommandState> CommandState(MessageId commandId) =>
            _commandStateHolders[commandId.Id].CommandState();

        /// <inheritdoc />
        public IObservable<bool> RetroactiveExecution => _messagesHolderDict
            .GetOrAdd(_timeline.Id, s => new UncompletedMessagesSingleHolder()).RetroactiveExecution();

        /// <inheritdoc />
        public IObservable<IEvent> Messages => _messages.AsObservable();

        /// <inheritdoc />
        public IObservable<IAlert> Alerts => _alerts.AsObservable();

        /// <inheritdoc />
        public IObservable<int> UncompletedMessagesOnBranch(string branchId) =>
            _messagesHolderDict.GetOrAdd(branchId, s => new UncompletedMessagesSingleHolder()).UncompletedMessages();

        /// <inheritdoc />
        public void Alert(IAlert alert)
        {
            _log.Trace(alert.GetType().GetFriendlyName(), this);
            _alerts.OnNext(alert);
        }

        /// <inheritdoc />
        public void Event(IEvent e)
        {
            _log.Trace(e.GetType().GetFriendlyName(), this);
            _messages.OnNext(e);
        }

        /// <inheritdoc />
        public async Task UncompleteCommand(MessageId commandId, bool isRetroactive = false)
        {
            if (commandId == default)
                return;

            var stateHolder = _commandStateHolders.GetOrAdd(commandId.Id, new CommandStateHolder());
            await stateHolder.UpdateState(b =>
            {
                b.Counter++;
                // _log.Debug($"Uncompleting command {commandId}: {b.Counter}");
                return b;
            }).ConfigureScheduler(TaskScheduler.Default);
        }
        
        /// <inheritdoc />
        public async Task FailCommand(MessageId commandId)
        {
            if (commandId == default)
                return;
            
            if (!_commandStateHolders.TryGetValue(commandId.Id, out var stateHolder))
                return;
            
            await stateHolder.UpdateState(b =>
            {
                b.HasFailed = true;
                return b;
            }).ConfigureScheduler(TaskScheduler.Default);
            
        }
        
        /// <inheritdoc />
        public async Task CompleteCommand(MessageId commandId)
        {
            if (commandId == default)
                return;

            if (!_commandStateHolders.TryGetValue(commandId.Id, out var stateHolder))
                return;
        
            await stateHolder.UpdateState(b =>
            {
                b.Counter--;
                //_log.Debug($"Completing command {commandId}: {b.Counter}");
                return b;
            }).ConfigureScheduler(TaskScheduler.Default);
        }
        
        /// <inheritdoc />
        public async Task CompleteMessage(IMessage message)
        {
            var messagesHolder =
                _messagesHolderDict.GetOrAdd(message.Timeline, s => new UncompletedMessagesSingleHolder());
            await messagesHolder.UpdateState(b =>
            {
                if (message.GetType().IsClosedTypeOf(typeof(RetroactiveCommand<>)))
                {
                    if (b.RetroactiveId != message.MessageId)
                    {
                        _log.Error($"Retroactive command {message.GetType().GetFriendlyName()} completed before being executed");
                        throw new InvalidOperationException("Retroactive command completed before being executed");
                    }

                    b.RetroactiveId = default;
                    
                    _log.Debug($"Completed retroactive command {message.Timeline}:{message.GetType().GetFriendlyName()} [{message.MessageId}] ({message.Timestamp.ToDateString()})");
                    return b;
                }

                b.Count--;
                if (b.Count < 0)
                    throw new InvalidOperationException($"Message {message.Timeline}:{message.GetType()} completed before being produced");

                _log.Debug($"Uncompleted messages : {b.Count}, removed {message.Timeline}:{message.GetType().GetFriendlyName()}");

                return b;
            }).ConfigureScheduler(TaskScheduler.Default);
        }

        /// <inheritdoc />
        public async Task UncompleteMessage(IMessage message)
        {
            var messagesHolder =
                _messagesHolderDict.GetOrAdd(message.Timeline, s => new UncompletedMessagesSingleHolder());
            await messagesHolder.UpdateState(b =>
            {
                if (message.GetType().IsClosedTypeOf(typeof(RetroactiveCommand<>)))
                {
                    if (b.RetroactiveId != default)
                    {
                        _log.Error($"Retroactive command still executing : {message.Timeline}:{message.GetType().GetFriendlyName()} [{message.MessageId}] ({message.Timestamp.ToDateString()})");
                        throw new InvalidOperationException("Retroactive command started before completing the previous one");
                    }

                    b.RetroactiveId = message.MessageId;
                    
                    _log.Debug($"Started retroactive execution {message.Timeline}:{message.GetType().GetFriendlyName()} [{message.MessageId}] ({message.Timestamp.ToDateString()})");
                    return b;
                }

                b.Timeline = message.Timeline;
                b.Count++;
                _log.Debug($"Uncompleted messages : {b.Count}, added {message.Timeline}:{message.GetType().GetFriendlyName()}");

                return b;
            }).ConfigureScheduler(TaskScheduler.Default);
        }
    }
}