using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Command decorator 
    /// </summary>
    /// <typeparam name="T">Command type</typeparam>
    public class CommandHandler<T> : ICommandHandler<T>
        where T : Command
    {
        private readonly ICommandHandler<T> _handler;
        private readonly ICommandLog _commandLog;
        private readonly ILog _log;
        private readonly IErrorLog _errorLog;
        private readonly ITimeline _timeline;
        private readonly IBranchManager _branchManager;
        private readonly IMessageQueue _messageQueue;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandHandler{T}"/> class.
        /// </summary>
        /// <param name="handler">Underlying handler to decorate</param>
        /// <param name="log">Application logger</param>
        /// <param name="timeline">Active timeline</param>
        /// <param name="commandLog">Command log</param>
        /// <param name="errorLog">Error log</param>
        /// <param name="branchManager">Branch manager</param>
        /// <param name="messageQueue">Message queue</param>
        public CommandHandler(ICommandHandler<T> handler, ILog log, ITimeline timeline, ICommandLog commandLog, IErrorLog errorLog, IBranchManager branchManager, IMessageQueue messageQueue)
        {
            _handler = handler;
            _log = log;
            _timeline = timeline;
            _commandLog = commandLog;
            _errorLog = errorLog;
            _branchManager = branchManager;
            _messageQueue = messageQueue;
        }

        /// <inheritdoc />
        public async Task Handle(ICommand command)
        {
            await Handle((T)command);
        }

        /// <inheritdoc />
        public bool CanHandle(ICommand command) => _handler.CanHandle(command);

        /// <inheritdoc />
        /// <summary>
        /// Wrap the handler and redirect all exception to <see cref="IErrorLog"/>
        /// </summary>
        public async Task Handle(T command)
        {
            _log.Trace($"{command.GetType().Name}", this);

            var timeline = _timeline.Id;
            if (command is not IRetroactiveCommand && command.Timestamp == default)
                command.Timestamp = _timeline.Now;
            command.LocalId ??= new EventId(Configuration.ReplicaName, command.Timestamp);
            command.OriginId ??= new EventId(Configuration.ReplicaName, command.Timestamp);
            command.Timeline = timeline;

            // check if command already processed
            if (command.StoreInLog)
            {
                if (await _commandLog.HasCommand(command))
                {
                    // _log.Warn($"Command {command.MessageType}:{command.MessageId} already exists in the command log");
                    _errorLog.Add(new InvalidOperationException($"Command {command.MessageType}:{command.MessageId} already exists in the command log"));
                    return;
                }
            }
            
            try
            {
                await _handler.Handle(command);
                if (command.StoreInLog && !command.Pure)
                    await _commandLog.AppendCommand(command);
            }
            catch (Exception e)
            {
                _errorLog.Add(e);
                
                // check that we didn't end up on wrong timeline
                if (_timeline.Id != timeline)
                {
                    var tException = new InvalidOperationException($"Execution started on {timeline} but ended on {_timeline.Id}");
                    _errorLog.Add(tException);
                    
                    // throw tException;
                    await _branchManager.Branch(timeline);
                }

                await _commandLog.AddFailedCommand(command);
                _log.Error($"Command {command} failed : {e}");
            }
        }
    }
}