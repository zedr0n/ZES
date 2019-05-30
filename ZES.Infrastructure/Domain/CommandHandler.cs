using System;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Command decorator 
    /// </summary>
    /// <typeparam name="T">Command type</typeparam>
    public class CommandHandler<T> : ICommandHandler<T>
        where T : ICommand
    {
        private readonly ICommandHandler<T> _handler;
        private readonly ICommandLog _commandLog;
        private readonly ILog _log;
        private readonly IErrorLog _errorLog;
        private readonly ITimeline _timeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandHandler{T}"/> class.
        /// </summary>
        /// <param name="handler">Underlying handler to decorate</param>
        /// <param name="log">Application logger</param>
        /// <param name="timeline">Active timeline</param>
        /// <param name="commandLog">Command log</param>
        /// <param name="errorLog">Error log</param>
        public CommandHandler(ICommandHandler<T> handler, ILog log, ITimeline timeline, ICommandLog commandLog, IErrorLog errorLog)
        {
            _handler = handler;
            _log = log;
            _timeline = timeline;
            _commandLog = commandLog;
            _errorLog = errorLog;
        }

        /// <inheritdoc />
        /// <summary>
        /// Wrap the handler and redirect all exception to <see cref="IErrorLog"/>
        /// </summary>
        public async Task Handle(T command)
        {
            _log.Trace($"{_handler.GetType().Name}.Handle({command.GetType().Name})");
            if (command is Command)
                (command as Command).Timestamp = _timeline.Now;
            
            try
            {
                await _handler.Handle(command);
            }
            catch (Exception e)
            {
                _errorLog.Add(e);
            }
            
            await _commandLog.AppendCommand(command);
        }
    }
}