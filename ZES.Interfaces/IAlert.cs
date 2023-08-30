namespace ZES.Interfaces
{
    /// <summary>
    /// Alerts are transient events which are not persisted to aggregate streams
    /// ( but can be persisted to e.g. sagas )
    /// </summary>
    public interface IAlert : IMessageEx<IMessageStaticMetadata, IMessageMetadata>
    {
    }
}