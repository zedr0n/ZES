using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;

namespace ZES
{
    /// <inheritdoc />
    public class MessageQueue : IMessageQueue
    {
        private readonly ILog _log;
        private readonly ITimeline _timeline;
        private readonly Subject<IEvent> _messages = new Subject<IEvent>();
        private readonly Subject<IAlert> _alerts = new Subject<IAlert>();
        private readonly BehaviorSubject<int> _uncompletedMessagesSubject = new BehaviorSubject<int>(0);
        private readonly ConcurrentDictionary<string, int> _uncompletedMessages = new ConcurrentDictionary<string, int>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageQueue"/> class.
        /// </summary>
        /// <param name="log">Application log</param>
        /// <param name="timeline">Timeline</param>
        public MessageQueue(ILog log, ITimeline timeline)
        {
            _log = log;
            _timeline = timeline;
        }

        /// <inheritdoc />
        public IObservable<int> UncompletedMessages => _uncompletedMessagesSubject.AsObservable(); 

        /// <inheritdoc />
        public IObservable<IEvent> Messages => _messages.AsObservable();

        /// <inheritdoc />
        public IObservable<IAlert> Alerts => _alerts.AsObservable();

        /// <inheritdoc />
        public void Alert(IAlert alert)
        {
            _log.Debug(alert.GetType().Name, this);
            _alerts.OnNext(alert);
        }

        /// <inheritdoc />
        public void Event(IEvent e)
        {
            _log.Debug(e.EventType, this);
            _messages.OnNext(e);
        }

        /// <inheritdoc />
        public void CompleteMessage(IMessage message)
        {
            // Interlocked.Decrement(ref _uncompletedMessages);
            var count = _uncompletedMessages.GetOrAdd(_timeline.Id, 0);
            if (!_uncompletedMessages.TryUpdate(_timeline.Id, count - 1, count))
               throw new InvalidOperationException("Concurrent update failed!");
            _log.Trace($"Uncompleted messages : {count - 1}, removed {_timeline.Id}:{message.GetType().Name}");
            lock (_uncompletedMessagesSubject)
                _uncompletedMessagesSubject.OnNext(count - 1);
        }

        /// <inheritdoc />
        public void UncompleteMessage(IMessage message)
        {
            var count = _uncompletedMessages.GetOrAdd(_timeline.Id, 0);
            if (!_uncompletedMessages.TryUpdate(_timeline.Id, count + 1, count))
                throw new InvalidOperationException("Concurrent update failed!");
            _log.Trace($"Uncompleted messages : {count + 1}, added {_timeline.Id}:{message.GetType().Name}");
            lock (_uncompletedMessagesSubject)
                _uncompletedMessagesSubject.OnNext(count + 1);
        }
    }
}