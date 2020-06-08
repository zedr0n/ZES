using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Projections
{
    internal class ProjectionBufferedDispatcher<TState> : Dataflow<Tracked<IStream>>
        where TState : new()
    {
        private readonly Dataflow<Tracked<IStream>, Tracked<IStream>> _buffer;
        private readonly ProjectionDispatcher<TState> _dispatcher;
        private readonly ILog _log;
        private CancellationToken _token;

        public ProjectionBufferedDispatcher(DataflowOptions dataflowOptions, ProjectionBase<TState> projection) 
            : base(dataflowOptions)
        {
            _buffer = new BufferBlock<Tracked<IStream>>().ToDataflow();
            _dispatcher = new ProjectionDispatcher<TState>(dataflowOptions, projection);
            _log = projection.Log;
            _token = projection.CancellationToken;
            _token.Register(() => _buffer.LinkTo(DataflowBlock.NullTarget<Tracked<IStream>>().ToDataflow()));

            RegisterChild(_buffer);
            RegisterChild(_dispatcher);
        }

        public override ITargetBlock<Tracked<IStream>> InputBlock => _buffer.InputBlock;
            
        public async Task Start()
        {
            var count = _buffer.BufferedCount;
            _log.Debug($"{count} streams in buffer", this);
                
            if (count > 0)
            {
                var obs = _buffer.OutputBlock.AsObservable()
                    .Take(count).Select(async s =>
                    {
                        await _dispatcher.SendAsync(s);
                        if (!_token.IsCancellationRequested) 
                            return await s.Task;
                        return false;
                    });

                await obs.LastOrDefaultAsync();
            }

            _buffer.LinkTo(_dispatcher, s => !_token.IsCancellationRequested);
            _buffer.LinkLeftToNull();
        }
    }
}