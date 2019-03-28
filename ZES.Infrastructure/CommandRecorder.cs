using System;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using SqlStreamStore.Logging;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ILog = ZES.Infrastructure.ILog;

namespace ZES.Infrastructure
{
    public class CommandRecorder<T> : ICommandHandler<T> where T : ICommand
    {
        private readonly ICommandHandler<T> _handler;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ILog _log;
        
        public CommandRecorder(ICommandHandler<T> handler, IEventStore<IAggregate> eventStore, ILog log)
        {
            _handler = handler;
            _eventStore = eventStore;
            _log = log;
        }

        public async Task Handle(T command)
        {
            //_log.Trace(command.GetType().Name,_handler); 
            _log.Trace($"{_handler.GetType().Name}.Handle({command.GetType().Name})");
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

            //command.Timestamp = _clock.GetCurrentInstant().ToUnixTimeMilliseconds();
            await _eventStore.AppendCommand(command);
        }
    }
}