using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;

namespace ZES.Infrastructure
{
    public abstract class TickingDataflow<TIn> : Dataflow<TIn, Task>
    {
        private readonly BufferBlock<Task> _outputBlock;

        public TickingDataflow(Dataflow<TIn> dataflow)
            : base(dataflow.DataflowOptions)
        {
            _outputBlock = new BufferBlock<Task>();
            RegisterChild(_outputBlock);
        }

        public override ISourceBlock<Task> OutputBlock => _outputBlock;
        protected async Task Next() => await _outputBlock.SendAsync(Task.CompletedTask);
    }
}