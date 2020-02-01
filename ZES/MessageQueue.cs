using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
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
        private readonly UncompletedMessagesHolder _messagesHolder; 

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageQueue"/> class.
        /// </summary>
        /// <param name="log">Application log</param>
        /// <param name="timeline">Timeline</param>
        public MessageQueue(ILog log, ITimeline timeline)
        {
            _log = log;
            _timeline = timeline;
            _messagesHolder = new UncompletedMessagesHolder();
        }

        /// <inheritdoc />
        public IObservable<int> UncompletedMessages => _messagesHolder.UncompletedMessages(_timeline.Id); 

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
        public async Task CompleteMessage(IMessage message)
        {
            await await _messagesHolder.UpdateState(b =>
            {
                if (!b.Count.TryGetValue(_timeline.Id, out var count))
                    throw new InvalidOperationException("Message completed before being produced");
                count--;
                b.Count[_timeline.Id] = count;
                _log.Trace($"Uncompleted messages : {count}, removed {_timeline.Id}:{message.GetType().Name}");

                return b;
            });
        }

        /// <inheritdoc />
        public async Task UncompleteMessage(IMessage message)
        {
            await await _messagesHolder.UpdateState(b =>
            {
                if (!b.Count.TryGetValue(_timeline.Id, out var count))
                    count = 0;
                    
                count++;
                b.Count[_timeline.Id] = count;
                _log.Trace($"Uncompleted messages : {count}, added {_timeline.Id}:{message.GetType().Name}");

                return b;
            });
        }
    }
}