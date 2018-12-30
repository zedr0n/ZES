using System;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;

namespace ZES.Core.Infrastructure
{
    public class CommandRecorder<T> : ICommandHandler<T> where T : ICommand
    {
        private readonly ICommandHandler<T> _handler;
        private readonly IEventStore _eventStore;
        
        public CommandRecorder(ICommandHandler<T> handler, IEventStore eventStore)
        {
            _handler = handler;
            _eventStore = eventStore;
        }

        public async Task Handle(T command)
        {
            //_log.WriteLine("Entering handler of " + command.GetType().Name);
            try
            {
                await _handler.Handle(command);
            }
            catch (Exception e)
            {
                //_log.WriteLine(e.Message);
                
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