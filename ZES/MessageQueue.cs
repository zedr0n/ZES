using System;
using System.Reactive.Linq;
using SqlStreamStore.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;

namespace ZES
{
    public class MessageQueue : IMessageQueue
    {
        private readonly ILog _log;
        private readonly Subject<IEvent> _messages = new Subject<IEvent>();
        private readonly Subject<IAlert> _alerts = new Subject<IAlert>(); 
        
        public MessageQueue(ILog log)
        {
            _log = log;
        }

        public IObservable<IEvent> Messages => _messages.AsObservable();
        public IObservable<IAlert> Alerts => _alerts.AsObservable();

        public void Alert(IAlert alert)
        {
            _log.Trace(alert.GetType().Name, this);
            _alerts.OnNext(alert);
        }

        public void Event(IEvent e)
        {
            _log.Trace(e.EventType, this);
            _messages.OnNext(e);
        }
    }
}