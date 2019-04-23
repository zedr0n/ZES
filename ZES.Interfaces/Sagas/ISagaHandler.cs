namespace ZES.Interfaces.Sagas
{
    /// <summary>
    /// Saga handler
    /// ( Sagas are transient so cannot own any services directly ) 
    /// </summary>
    /// <typeparam name="TSaga">Saga type</typeparam>
    public interface ISagaHandler<TSaga>
        where TSaga : class, ISaga, new()
    {
    }
}