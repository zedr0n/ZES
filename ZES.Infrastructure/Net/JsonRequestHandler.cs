using System.Threading.Tasks;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Net;
using ZES.Interfaces.Pipes;

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
            res.ContinueWith(r => _messageQueue.Alert(new JsonRequestCompleted(r.Result)));
        }

        /// <inheritdoc />
        public async Task Handle(ICommand command) => await Handle(command as RequestJson);
    }
}