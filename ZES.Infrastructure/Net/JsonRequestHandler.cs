using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Net;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Net
{
    /// <summary>
    /// Json request command handler
    /// </summary>
    public class JsonRequestHandler : CommandHandlerAbstractBase<RequestJson>
    {
        private readonly IJSonConnector _connector;
        private readonly IMessageQueue _messageQueue;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRequestHandler"/> class.
        /// </summary>
        /// <param name="connector">JSON connector service</param>
        /// <param name="messageQueue">Message queue</param>
        public JsonRequestHandler(IJSonConnector connector, IMessageQueue messageQueue)
        {
            _connector = connector;
            _messageQueue = messageQueue;
        }

        /// <inheritdoc />
        public override async Task Handle(RequestJson command)
        {
            var res = await _connector.SubmitRequest(command.Url);
            var alert = new JsonRequestSubmitted(command.Target, command.Url) { Timeline = command.Timeline };
            _messageQueue.Alert(alert);
            _messageQueue.UncompleteMessage(alert);
            res.ContinueWith(r =>
            {
                _messageQueue.CompleteMessage(alert);
                _messageQueue.Alert(new JsonRequestCompleted(command.Target, command.Url, r.Result));
            });
        }
    }

    /// <summary>
    /// JSON deserialized request command handler
    /// </summary>
    /// <typeparam name="T">Deserialized type</typeparam>
    public class JsonRequestHandler<T> : CommandHandlerAbstractBase<RequestJson<T>>
        where T : class, IJsonResult
    {
        private readonly IMessageQueue _messageQueue;
        private readonly ISerializer<T> _serializer;
        private readonly ICommandHandler<RequestJson> _handler;
        private readonly IJsonHandler<T> _jsonHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRequestHandler{T}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="serializer">JSON deserializer</param>
        /// <param name="handler">JSON request connection handler</param>
        /// <param name="jsonHandler">JSON result handler</param>
        public JsonRequestHandler(IMessageQueue messageQueue, ISerializer<T> serializer, ICommandHandler<RequestJson> handler, IJsonHandler<T> jsonHandler) 
        {
            _messageQueue = messageQueue;
            _serializer = serializer;
            _handler = handler;
            _jsonHandler = jsonHandler;
        }
        
        /// <inheritdoc />
        public override async Task Handle(RequestJson<T> command)
        {
            _messageQueue.Alerts.OfType<JsonRequestCompleted>()
                .Where(r => r.Url == command.Url)
                .Subscribe(r =>
                {
                    T o = null;
                    if (r.JsonData != null)
                    {
                        o = _serializer.Deserialize(r.JsonData);
                        o.RequestorId = r.RequestorId;
                    }

                    var alert = new JsonRequestCompleted<T>(r.RequestorId, r.Url, o) { Timeline = command.Timeline };
                    _messageQueue.Alert(alert);
                    PostEvents(o, command);
                });
            await _handler.Handle(command);
        }

        private void PostEvents(T response, ICommand command)
        {
            var events = _jsonHandler.Handle(response);
            if (events == null) 
                return;

            foreach (var e in events.Cast<Event>())
            {
                e.CommandId = command.MessageId;
                e.Timeline = command.Timeline;
                _messageQueue.Event(e);
            }
        }
    }
}