namespace ZES.Interfaces
{
    public interface IError
    {
        string Message { get; }
        long? Timestamp { get; }
    }
}