using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;

namespace ZES
{
    /// <inheritdoc />
    public class MessageQueue : IMessageQueue
    {
        private readonly ILog _log;
        private readonly Subject<IEvent> _messages = new Subject<IEvent>();
        private readonly Subject<IAlert> _alerts = new Subject<IAlert>();

        private int _uncompletedMessages;
        private readonly BehaviorSubject<int> _uncompletedMessagesSubject = new BehaviorSubject<int>(0);
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageQueue"/> class.
        /// </summary>
        /// <param name="log">Application log</param>
        public MessageQueue(ILog log)
        {
            _log = log;
        }

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

        public async Task CompleteMessage(IMessage message)
        {
            Interlocked.Decrement(ref _uncompletedMessages);
            _log.Trace($"Uncompleted messages : {_uncompletedMessages}, removing {message.GetType().Name}");
            lock (_uncompletedMessagesSubject)
                _uncompletedMessagesSubject.OnNext(_uncompletedMessages);
        }

        public void UncompleteMessage(IMessage message)
        {
            Interlocked.Increment(ref _uncompletedMessages);
            _log.Trace($"Uncompleted messages : {_uncompletedMessages}, adding {message.GetType().Name}");
            lock (_uncompletedMessagesSubject)
                _uncompletedMessagesSubject.OnNext(_uncompletedMessages);
        }
    }
}