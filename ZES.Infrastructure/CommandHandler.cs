using System;
using System.Threading.Tasks;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure
{
    public class CommandHandler<T> : ICommandHandler<T> where T : ICommand
    {
        private readonly ICommandHandler<T> _handler;
        private readonly ICommandLog _commandLog;
        private readonly ILog _log;
        private readonly ITimeline _timeline;
        
        public CommandHandler(ICommandHandler<T> handler, ILog log, ITimeline timeline, ICommandLog commandLog)
        {
            _handler = handler;
            _log = log;
            _timeline = timeline;
            _commandLog = commandLog;
        }

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
                _log.Error(e.Message);
                
                // retry the command in case of concurrency exception
                //if(e is ConcurrencyException)
                //    await _handler.Handle(command);
                throw;
            }

            await _commandLog.AppendCommand(command);
        }
    }
}