using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using SimpleInjector;
using ZES.Infrastructure.Domain;
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
        public IObservable<IEvent> Messages => _messages.AsObservable();

        /// <inheritdoc />
        public IObservable<IAlert> Alerts => _alerts.AsObservable();

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
    }
}