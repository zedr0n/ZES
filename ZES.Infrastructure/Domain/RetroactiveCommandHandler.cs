using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
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
        private readonly ILog _log;
        private readonly IMessageQueue _messageQueue;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetroactiveCommandHandler{TCommand}"/> class.
        /// </summary>
        /// <param name="retroactive">Retroactive functional</param>
        /// <param name="commandLog">Command log</param>
        /// <param name="log">Application log</param>
        /// <param name="messageQueue">Message queue</param>
        public RetroactiveCommandHandler(IRetroactive retroactive, ICommandLog commandLog, ILog log, IMessageQueue messageQueue) 
        {
            _retroactive = retroactive;
            _commandLog = commandLog;
            _log = log;
            _messageQueue = messageQueue;
        }

        /// <inheritdoc />
        public async Task Handle(RetroactiveCommand<TCommand> iCommand)
        {
            _log.StopWatch.Start($"{nameof(RetroactiveCommandHandler<TCommand>)}");
            iCommand.Command.Timestamp = iCommand.Timestamp;
            iCommand.Command.ForceTimestamp();
            var time = iCommand.Timestamp;

            _log.StopWatch.Start("GetChanges_1");
            var changes = await _retroactive.GetChanges(iCommand.Command, time);
            _log.StopWatch.Stop("GetChanges_1");
           
            _log.StopWatch.Start("TryInsert_1");
            var invalidEvents = await _retroactive.TryInsert(changes, time); 
            _log.StopWatch.Stop("TryInsert_1");

            if (invalidEvents.Count > 0)
            {
                var commands = await RollbackEvents(invalidEvents);
                changes = await _retroactive.GetChanges(iCommand.Command, time);
                await _retroactive.TryInsert(changes, time);

                foreach (var c in commands)
                {
                    _log.Info($"Replaying command {c.GetType().GetFriendlyName()} with timestamp {c.Timestamp.ToDateString()}");
                    await _retroactive.ReplayCommand(c);
                }
            }

            await _commandLog.AppendCommand(iCommand.Command);
            iCommand.EventType = iCommand.Command.EventType;
            _messageQueue.Alert(new InvalidateProjections());
            _log.StopWatch.Stop($"{nameof(RetroactiveCommandHandler<TCommand>)}");
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
                _log.Warn($"Invalid event found in stream {e.Stream} : {e.MessageType}@{e.Version}", this);
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