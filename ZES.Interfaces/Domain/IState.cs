namespace ZES.Interfaces.Domain
{
    /// <summary>
    /// State interface
    /// </summary>
    public interface IState
    {
    }

    /// <summary>
    /// Marker interface for projection states that can be materialized at additional historical timestamps.
    /// </summary>
    public interface IHistoricalState : IState
    {
    }
}
