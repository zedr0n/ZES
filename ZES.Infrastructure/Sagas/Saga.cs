using System.Collections.Generic;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Sagas;

namespace ZES.Infrastructure.Sagas
{
    /// <inheritdoc cref="ISaga" />
    public class Saga : EventSourced, ISaga
    {
        private readonly List<ICommand> _undispatchedCommands = new List<ICommand>();

        /// <summary>
        /// Saga mapper
        /// </summary>
        /// <param name="e">Event</param>
        /// <returns>Saga identifier</returns>
        public static string SagaId(IEvent e) => null;

        /// <inheritdoc />
        public IEnumerable<ICommand> GetUncommittedCommands()
        {
            lock (_undispatchedCommands)
                return _undispatchedCommands.ToArray();
        }

        /// <inheritdoc />
        public void SendCommand(ICommand command)
        {
            lock (_undispatchedCommands)
                _undispatchedCommands.Add(command);
        }

        /// <inheritdoc />
        public override void LoadFrom<T>(IEnumerable<IEvent> pastEvents)
        {
            base.LoadFrom<T>(pastEvents);
            ClearUncommittedCommands();
        }

        private void ClearUncommittedCommands()
        {
            lock (_undispatchedCommands) 
                _undispatchedCommands.Clear();
        }
    }   
}