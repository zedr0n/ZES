using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Serialization
{
    public interface ISerializer<T> where T : class
    {
        string Serialize(T e);
        T Deserialize(string json);
    }

    public interface IEventSerializer : ISerializer<IEvent>
    {
    }

    public interface ICommandSerializer : ISerializer<ICommand>
    {
        
    }
}