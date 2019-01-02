using System;
using System.Threading.Tasks;
using SqlStreamStore.Logging;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure
{
    public class CommandRecorder<T> : ICommandHandler<T> where T : ICommand
    {
        private readonly ICommandHandler<T> _handler;
        private readonly IEventStore _eventStore;
        private readonly ILog _log;
        
        public CommandRecorder(ICommandHandler<T> handler, IEventStore eventStore, ILog log)
        {
            _handler = handler;
            _eventStore = eventStore;
            _log = log;
        }

        public async Task Handle(T command)
        {
            _log.Info("Entering handler of " + command.GetType().Name);
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