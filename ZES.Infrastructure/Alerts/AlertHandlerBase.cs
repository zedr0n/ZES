using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Alerts
{
    /// <inheritdoc />
    public abstract class AlertHandlerBase<TAlert> : IAlertHandler<TAlert>
        where TAlert : class, IAlert
    {
        private readonly IMessageQueue _messageQueue;

        /// <summary>
        /// Initializes a new instance of the <see cref="AlertHandlerBase{TAlert}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message query</param>
        public AlertHandlerBase(IMessageQueue messageQueue)
        {
            _messageQueue = messageQueue;
            _messageQueue.Alerts.OfType<TAlert>().Subscribe(async a => await Handle(a));
        }

        /// <inheritdoc />
        public async Task Handle(TAlert alert)
        {
            var events = await Process(alert);
            foreach (var e in events)
                _messageQueue.Event(e);
        }

        /// <inheritdoc />
        public async Task Handle(IAlert alert)
        {
            await Handle(alert as TAlert);
        }

        /// <summary>
        /// Process the alert
        /// </summary>
        /// <param name="alert">Alert to handle</param>
        /// <returns>Resulting event</returns>
        protected abstract Task<IEnumerable<IEvent>> Process(TAlert alert);
    }
}