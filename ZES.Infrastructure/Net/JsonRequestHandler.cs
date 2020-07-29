using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Net;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Net
{
    /// <summary>
    /// Json request command handler
    /// </summary>
    public class JsonRequestHandler : ICommandHandler<RequestJson>
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
        public async Task Handle(RequestJson command)
        {
            command.EventType = "JsonRequest";
            var res = await _connector.SubmitRequest(command.Url);
            res.ContinueWith(r => _messageQueue.Alert(new JsonRequestCompleted(command.Url, r.Result)));
        }

        /// <inheritdoc />
        public async Task Handle(ICommand command) => await Handle(command as RequestJson);
    }

    /// <summary>
    /// JSON deserialized request command handler
    /// </summary>
    /// <typeparam name="T">Deserialized type</typeparam>
    public class JsonRequestHandler<T> : ICommandHandler<RequestJson<T>>
        where T : class, IJsonResult
    {
        private readonly IMessageQueue _messageQueue;
        private readonly ISerializer<T> _serializer;
        private readonly ICommandHandler<RequestJson> _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRequestHandler{T}"/> class.
        /// </summary>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="serializer">JSON deserializer</param>
        /// <param name="handler">JSON request connection handler</param>
        public JsonRequestHandler(IMessageQueue messageQueue, ISerializer<T> serializer, ICommandHandler<RequestJson> handler) 
        {
            _messageQueue = messageQueue;
            _serializer = serializer;
            _handler = handler;
        }
        
        /// <inheritdoc />
        public async Task Handle(RequestJson<T> command)
        {
            _messageQueue.Alerts.OfType<JsonRequestCompleted>()
                .Where(r => r.Url == command.Url)
                .Subscribe(r =>
                {
                    var o = _serializer.Deserialize(r.JsonData);
                    _messageQueue.Alert(new JsonRequestCompleted<T>(r.Url, o));
                });
            await _handler.Handle(command);
        }

        /// <inheritdoc />
        public async Task Handle(ICommand command) => await Handle(command as RequestJson<T>);
    }
}