using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using SqlStreamStore.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class RetroactiveCommandHandler<TCommand, TRoot> : ICommandHandler<RetroactiveCommand<TCommand>>
        where TCommand : Command 
        where TRoot : IAggregate
    {
        private readonly ICommandHandler<TCommand> _handler;
        private readonly IBranchManager _manager;
        private readonly IRetroactive _retroactive;
        private readonly IStreamLocator _streamLocator;
        private readonly IEventStore<IAggregate> _eventStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="RetroactiveCommandHandler{TCommand, TRoot}"/> class.
        /// </summary>
        /// <param name="handler">Underlying command handler</param>
        /// <param name="manager">Branch manager</param>
        /// <param name="retroactive">Retroactive functional</param>
        /// <param name="eventStore">Event store</param>
        /// <param name="streamLocator">Stream locator service</param>
        public RetroactiveCommandHandler(
            ICommandHandler<TCommand> handler, 
            IBranchManager manager, 
            IRetroactive retroactive,
            IEventStore<IAggregate> eventStore,
            IStreamLocator streamLocator) 
        {
            _handler = handler;
            _manager = manager;
            _retroactive = retroactive;
            _eventStore = eventStore;
            _streamLocator = streamLocator;
        }

        /// <inheritdoc />
        public async Task Handle(RetroactiveCommand<TCommand> iCommand)
        {
            iCommand.Command.Timestamp = iCommand.Timestamp;
            iCommand.Command.ForceTimestamp();

            var activeBranch = _manager.ActiveBranch;
            var time = iCommand.Timestamp;
            var branch = $"{typeof(TCommand).Name}-{time}";

            await _manager.Branch(branch, time);
            await _handler.Handle(iCommand.Command);
            await _manager.Branch(activeBranch);
            
            var changes = await _manager.GetChanges(branch);
            var canInsert = true;
            foreach (var c in changes)
            {
                var stream = _streamLocator.FindBranched(c.Key, branch);
                var e = await _eventStore.ReadStream<IEvent>(stream, stream.Version - c.Value + 1, c.Value).ToList();

                canInsert &= await _retroactive.CanInsertIntoStream(c.Key, c.Key.Version + 1, e);
            }

            if (canInsert)
            {
                foreach (var c in changes)
                {
                    var stream = _streamLocator.FindBranched(c.Key, branch);
                    var e = await _eventStore.ReadStream<IEvent>(stream, stream.Version - c.Value + 1, c.Value).ToList();

                    await _retroactive.TryInsertIntoStream(c.Key, c.Key.Version + 1, e);
                }
            }
                
            await _manager.DeleteBranch(branch);
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
    }
}