using System.Collections.Generic;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Sagas
{
    public class Saga : EventSourced, ISaga
    {
        private readonly List<ICommand> _undispatchedCommands = new List<ICommand>();

        private void ClearUncommittedCommands()
        {
            lock(_undispatchedCommands) 
                _undispatchedCommands.Clear();
        }
        
        public ICommand[] GetUncommittedCommands()
        {
            lock (_undispatchedCommands)
                return _undispatchedCommands.ToArray();
        }

        public void SendCommand(ICommand command)
        {
            lock(_undispatchedCommands)
                _undispatchedCommands.Add(command);
        }

        public override void LoadFrom<T>(IEnumerable<IEvent> pastEvents)
        {
            base.LoadFrom<T>(pastEvents);
            ClearUncommittedCommands();
        }
    }   
}