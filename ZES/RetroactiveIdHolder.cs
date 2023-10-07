using System;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;

namespace ZES;

/// <summary>
/// State holder for retroactive execution
/// </summary>
public class RetroactiveIdHolder : StateHolder<RetroactiveIdHolder.State, RetroactiveIdHolder.Builder>
{
    /// <summary>
    /// Gets the observable indicating whether retroactive execution is active
    /// </summary>
    /// <returns></returns>
    public IObservable<bool> RetroactiveExecution()
    {
        return Project(x => x.Counter > 0);
    }
    
    /// <summary>
    /// Held state
    /// </summary>
    /// <param name="CommandId">Retroactive command id</param>
    /// <param name="Counter">Completion counter</param>
    public record struct State(Guid CommandId, int Counter) {}

    /// <inheritdoc />
    public struct Builder : IHeldStateBuilder<State, Builder>
    {
        /// <summary>
        /// Retroactive command id
        /// </summary>
        public Guid CommandId { get; set; }
        
        /// <summary>
        /// Completion counter
        /// </summary>
        public int Counter { get; set; }

        /// <inheritdoc />
        public void InitializeFrom(State state)
        {
            CommandId = state.CommandId;
            Counter = state.Counter;
        }

        /// <inheritdoc />
        public State Build() => new(CommandId, Counter);

        /// <inheritdoc />
        public State DefaultState() => new();
    }
}