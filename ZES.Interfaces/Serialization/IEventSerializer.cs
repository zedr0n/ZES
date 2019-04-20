using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Serialization
{
    public interface ISerializer<T> where T : class
    {
        string Serialize(T e);
        string Metadata(long? timestamp);
        T Deserialize(string json);
    }

    public interface IEventSerializer : ISerializer<IEvent>
    {
    }

    public interface ICommandSerializer : ISerializer<ICommand>
    {
        
    }
}