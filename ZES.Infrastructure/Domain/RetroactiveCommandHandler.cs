using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
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
        private readonly IStreamLocator<IAggregate> _streamLocator;
        private readonly IEventStore<IAggregate> _eventStore;
        private readonly IBus _bus;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="RetroactiveCommandHandler{TCommand, TRoot}"/> class.
        /// </summary>
        /// <param name="handler">Underlying command handler</param>
        /// <param name="manager">Branch manager</param>
        /// <param name="retroactive">Retroactive functional</param>
        /// <param name="eventStore">Event store</param>
        /// <param name="bus">Command bus</param>
        /// <param name="streamLocator">Stream locator service</param>
        public RetroactiveCommandHandler(
            ICommandHandler<TCommand> handler, 
            IBranchManager manager, 
            IRetroactive retroactive,
            IEventStore<IAggregate> eventStore,
            IBus bus,
            IStreamLocator<IAggregate> streamLocator) 
        {
            _handler = handler;
            _manager = manager;
            _retroactive = retroactive;
            _eventStore = eventStore;
            _bus = bus;
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

            if (!await _handler.IsRetroactive(iCommand.Command))
            {
                await _manager.Branch(branch, time);
                await _handler.Handle(iCommand.Command);
                await _manager.Branch(activeBranch);
                await _manager.Merge(branch);
                await _manager.DeleteBranch(branch);
                return;
            } 
            
            await _manager.Branch(branch, time - 1);
            
            var stream = _streamLocator.Find<TRoot>(iCommand.Target, branch);
            var prevVersion = stream.Version;
            await _handler.Handle(iCommand.Command);
            stream = _streamLocator.Find<TRoot>(iCommand.Target, branch);
            var e = await _eventStore.ReadStream<IEvent>(stream, prevVersion + 1, stream.Version - prevVersion + 1).ToList();

            await _manager.Branch(activeBranch);
            stream = _streamLocator.Find<TRoot>(iCommand.Target, activeBranch);
            await _retroactive.InsertIntoStream(stream, prevVersion + 1, e);

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