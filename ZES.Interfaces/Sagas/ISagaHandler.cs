namespace ZES.Interfaces.Sagas
{
    public interface ISagaHandler<TSaga>
        where TSaga : class, ISaga, new()
    {
    }
}