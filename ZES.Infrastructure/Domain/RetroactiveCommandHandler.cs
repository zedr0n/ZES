using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class RetroactiveCommandHandler<TCommand> : ICommandHandler<RetroactiveCommand<TCommand>>
        where TCommand : Command 
    {
        private readonly IRetroactive _retroactive;
        private readonly ICommandLog _commandLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetroactiveCommandHandler{TCommand}"/> class.
        /// </summary>
        /// <param name="retroactive">Retroactive functional</param>
        /// <param name="commandLog">Command log</param>
        public RetroactiveCommandHandler(IRetroactive retroactive, ICommandLog commandLog) 
        {
            _retroactive = retroactive;
            _commandLog = commandLog;
        }

        /// <inheritdoc />
        public async Task Handle(RetroactiveCommand<TCommand> iCommand)
        {
            iCommand.Command.Timestamp = iCommand.Timestamp;
            iCommand.Command.ForceTimestamp();
            var time = iCommand.Timestamp;

            var changes = await _retroactive.GetChanges(iCommand.Command, time);

            var invalidEvents = new List<IEvent>();
            foreach (var c in changes)
            {
                var events = c.Value;
                var invalidEvent = await _retroactive.ValidateInsert(c.Key, c.Key.Version + 1, events);
                if (invalidEvent != null)
                    invalidEvents.AddRange(invalidEvent);
            }

            var commands = await RollbackEvents(invalidEvents);
        
            if (invalidEvents.Count > 0)    
                changes = await _retroactive.GetChanges(iCommand.Command, time);
            
            foreach (var c in changes)
            {
                var events = c.Value;
                await _retroactive.TryInsertIntoStream(c.Key, c.Key.Version + 1, events);
                await _commandLog.AppendCommand(iCommand.Command);
            }

            foreach (var c in commands)
                await _retroactive.ReplayCommand(c);

            iCommand.EventType = iCommand.Command.EventType;
        }

        /// <inheritdoc />
        public Task<bool> IsRetroactive(RetroactiveCommand<TCommand> iCommand)
        {
            throw new InvalidOperationException("Retroactive commands cannot be nested");
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
                var c = await _commandLog.GetCommand(e);
                
                if (!commands.ContainsKey(e.Stream))
                    commands[e.Stream] = new List<ICommand>();
                
                if (commands[e.Stream].All(x => x.MessageId != c.MessageId))
                    commands[e.Stream].Add(c);
            }

            foreach (var s in commands.Keys)
                await _retroactive.RollbackCommands(commands[s]);

            return commands.Values.SelectMany(v => v).OrderBy(v => v.Timestamp);
        }
    }
}