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
        private readonly RetroactiveIdHolder _retroactiveIdHolder;

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
            _retroactiveIdHolder = new RetroactiveIdHolder();
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
        public IObservable<bool> RetroactiveExecution =>
            _retroactiveIdHolder.RetroactiveExecution();
            // _messagesHolderDict.GetOrAdd(_timeline.Id, s => new UncompletedMessagesSingleHolder()).RetroactiveExecution();

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
            await _retroactiveIdHolder.UpdateState(b =>
            {
                if (isRetroactive && b.Counter == 0)
                    b.CommandId = commandId.Id;

                if (b.CommandId == commandId.Id)
                    b.Counter++;
                else if (isRetroactive)
                {
                    var state = default(CommandState);
                    if (_commandStateHolders.TryGetValue(b.CommandId, out stateHolder))
                        state = stateHolder.CommandState().Current();

                    throw new InvalidOperationException(
                        $"Retroactive execution already on for {b.CommandId} with state {state} while trying to add {commandId.MessageType}:{commandId.Id}");
                }

                return b;
            }).ConfigureScheduler(TaskScheduler.Default);
            
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
        
            await _retroactiveIdHolder.UpdateState(b =>
            {
                if (b.CommandId == commandId.Id)
                    b.Counter--;
                    
                return b;
            }).ConfigureScheduler(TaskScheduler.Default);
            
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
            if (message?.Timeline == null)
                return;
            
            var messagesHolder =
                _messagesHolderDict.GetOrAdd(message.Timeline, s => new UncompletedMessagesSingleHolder());
            await messagesHolder.UpdateState(b =>
            {
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
            if (message?.Timeline == null)
                return;
            
            var messagesHolder =
                _messagesHolderDict.GetOrAdd(message.Timeline, s => new UncompletedMessagesSingleHolder());
            await messagesHolder.UpdateState(b =>
            {
                b.Timeline = message.Timeline;
                b.Count++;
                _log.Debug($"Uncompleted messages : {b.Count}, added {message.Timeline}:{message.GetType().GetFriendlyName()}");

                return b;
            }).ConfigureScheduler(TaskScheduler.Default);
        }
    }
}