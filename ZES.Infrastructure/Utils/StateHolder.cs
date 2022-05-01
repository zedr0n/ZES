using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ZES.Infrastructure.Utils
{
  /// <summary>
  /// Async state holder
  /// </summary>
  /// <typeparam name="THeldState">Held state type</typeparam>
  /// <typeparam name="THeldStateBuilder">Held state builder</typeparam>
  public abstract class StateHolder<THeldState, THeldStateBuilder> 
    where THeldState : struct
    where THeldStateBuilder : struct, IHeldStateBuilder<THeldState, THeldStateBuilder>
  {
    private readonly BehaviorSubject<THeldState> _currentStateSubject = new BehaviorSubject<THeldState>(default(THeldStateBuilder).DefaultState());
    private readonly ActionBlock<Tracked<TransactionFunc>> _transactionBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateHolder{THeldState, THeldStateBuilder}"/> class.
    /// </summary>
    protected StateHolder()
    {
      _transactionBuffer = new ActionBlock<Tracked<TransactionFunc>>(
        transaction =>
        {
          // This block will run serially because MaxDegreeOfParallelism is 1
          // That means we can read from and modify the current state (held in the subject) atomically
          transaction.Value(_currentStateSubject);
          transaction.Complete();
        }, Configuration.DataflowOptions.ToDataflowBlockOptions(false)); // new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
      CurrentState = _currentStateSubject.DistinctUntilChanged();
    }
    
    private delegate void TransactionFunc(BehaviorSubject<THeldState> heldStateSubject);

    /// <summary>
    /// Gets observable for current value of the state
    /// </summary>
    public IObservable<THeldState> CurrentState { get; }

    /// <summary>
    /// Asynchronously update the state
    /// </summary>
    /// <param name="updateBlock">Builder update block</param>
    /// <returns>Returns the task representing the update of the state</returns>
    public async Task UpdateState(Func<THeldStateBuilder, THeldStateBuilder> updateBlock)
    {
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
      }

      var tracked = new Tracked<TransactionFunc>(UpdateTransaction);
      var didSend = await _transactionBuffer.SendAsync(tracked);

      if (!didSend)
      {
        throw new ApplicationException(
          "UpdateState failed to process transaction. This probably means the BufferBlock is not initialized properly");
      }

      await tracked.Task;
    }

    /// <summary>
    /// Project the sub-values of the state 
    /// </summary>
    /// <param name="block">Sub-value projection</param>
    /// <typeparam name="T">Sub-value type</typeparam>
    /// <returns>Observable representing the sub-value</returns>
    public IObservable<T> Project<T>(Func<THeldState, T> block)
    {
      return CurrentState.Select(block).DistinctUntilChanged();
    }
  }
}
