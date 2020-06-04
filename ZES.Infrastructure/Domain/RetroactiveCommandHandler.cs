using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Retroaction;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class RetroactiveCommandHandler<TCommand> : ICommandHandler<RetroactiveCommand<TCommand>>
        where TCommand : Command 
    {
        private readonly IRetroactive _retroactive;
        private readonly ICommandLog _commandLog;
        private readonly ILog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetroactiveCommandHandler{TCommand}"/> class.
        /// </summary>
        /// <param name="retroactive">Retroactive functional</param>
        /// <param name="commandLog">Command log</param>
        /// <param name="log">Application log</param>
        public RetroactiveCommandHandler(IRetroactive retroactive, ICommandLog commandLog, ILog log) 
        {
            _retroactive = retroactive;
            _commandLog = commandLog;
            _log = log;
        }

        /// <inheritdoc />
        public async Task Handle(RetroactiveCommand<TCommand> iCommand)
        {
            iCommand.Command.Timestamp = iCommand.Timestamp;
            iCommand.Command.ForceTimestamp();
            var time = iCommand.Timestamp;

            var changes = await _retroactive.GetChanges(iCommand.Command, time);
            var invalidEvents = await _retroactive.TryInsert(changes, time); 

            if (invalidEvents.Count > 0)
            {
                var commands = await RollbackEvents(invalidEvents);
                changes = await _retroactive.GetChanges(iCommand.Command, time);
                await _retroactive.TryInsert(changes, time);
                
                foreach (var c in commands)
                    await _retroactive.ReplayCommand(c);
            }
                
            await _commandLog.AppendCommand(iCommand.Command);
            iCommand.EventType = iCommand.Command.EventType;
        }

        /// <inheritdoc />
        public async Task Handle(ICommand command)
        {
            await Handle((RetroactiveCommand<TCommand>)command);
        }

        private async Task<IEnumerable<ICommand>> RollbackEvents(IEnumerable<IEvent> invalidEvents)
        {
            var commands = new Dictionary<string, List<ICommand>>();
            foreach (var e in invalidEvents)
            {
                _log.Warn($"Invalid event found in stream {e.Stream} : {e.EventType}@{e.Version}", this);
                var c = await _commandLog.GetCommand(e);
                
                if (!commands.ContainsKey(e.Stream))
                    commands[e.Stream] = new List<ICommand>();
                
                if (commands[e.Stream].All(x => x.MessageId != c.MessageId))
                    commands[e.Stream].Add(c);
            }

            foreach (var s in commands.Keys)
            {
                if (!await _retroactive.RollbackCommands(commands[s]))
                    throw new InvalidOperationException($"Cannot rollback command {commands[s].GetType().Namespace}");
            }

            return commands.Values.SelectMany(v => v).OrderBy(v => v.Timestamp);
        }
    }
}