namespace ZES.Interfaces.Serialization
{
    public interface ISerializer<T>
        where T : class
    {
        string Serialize(T e);
        string Metadata(long? timestamp);
        T Deserialize(string json);
    }
}