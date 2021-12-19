namespace ZES.Interfaces
{
    public interface IFactory<T>
        where T : class
    {
        T Create();
    }
}