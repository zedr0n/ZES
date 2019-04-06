using System;
using System.Threading.Tasks;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure
{
    public class CommandRecorder<T> : ICommandHandler<T> where T : ICommand
    {
        private readonly ICommandHandler<T> _handler;
        private readonly ICommandLog _commandLog;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ILog _log;
        private readonly ITimeline _timeline;
        
        public CommandRecorder(ICommandHandler<T> handler, IEventStore<IAggregate> eventStore, ILog log, ITimeline timeline, ICommandLog commandLog)
        {
            _handler = handler;
            _eventStore = eventStore;
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