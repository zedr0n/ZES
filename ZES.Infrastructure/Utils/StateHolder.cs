using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ZES.Infrastructure.Utils
{
  public abstract class StateHolder<THeldState, THeldStateBuilder> 
    where THeldState : struct
    where THeldStateBuilder : struct, IHeldStateBuilder<THeldState, THeldStateBuilder>
  {
    private delegate void TransactionFunc(BehaviorSubject<THeldState> heldStateSubject);

    private readonly BehaviorSubject<THeldState> _currentStateSubject = new BehaviorSubject<THeldState>(new THeldStateBuilder().DefaultState());
    private readonly ActionBlock<TransactionFunc> _transactionBuffer;

    public THeldState LatestState => _currentStateSubject.Value;
    public IObservable<THeldState> CurrentState { get; private set; }
    public StateHolder()
    {
      _transactionBuffer = new ActionBlock<TransactionFunc>(
        transaction =>
        {
          // This block will run serially because MaxDegreeOfParallelism is 1
          // That means we can read from and modify the current state (held in the subject) atomically
          transaction(_currentStateSubject);
        }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
      CurrentState = _currentStateSubject.DistinctUntilChanged();
    }

    public async Task<Task> UpdateState(Func<THeldStateBuilder, THeldStateBuilder> updateBlock)
    {
      var taskCompletionSource = new TaskCompletionSource<Unit>();

      void UpdateTransaction(BehaviorSubject<THeldState> currentStateSubject)
      {
        var builder = default(THeldStateBuilder);
        builder.InitializeFrom(currentStateSubject.Value);
        try
        {
          var newState = updateBlock(builder).Build();
          currentStateSubject.OnNext(newState);
        }
        catch (Exception ex)
        {
          Console.WriteLine("ERROR: Update state transaction failed");
          Console.WriteLine(ex);
        }

        taskCompletionSource.SetResult(Unit.Default);
      }

      var didSend = await _transactionBuffer.SendAsync(UpdateTransaction);

      if (!didSend)
      {
        throw new ApplicationException(
          "UpdateState failed to process transaction. This probably means the BufferBlock is not initialized properly");
      }

      return taskCompletionSource.Task;
    }

    // Use this in xStateHolder class to make observable projections of elements in the state.
    public IObservable<T> Project<T>(Func<THeldState, T> block)
    {
      return CurrentState.Select(block).DistinctUntilChanged();
    }
  }
}
