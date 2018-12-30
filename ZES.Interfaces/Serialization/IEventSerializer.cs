namespace ZES.Interfaces.Serialization
{

    public interface IEventSerializer
    {
        string Serialize(IEvent e);

        IEvent Deserialize(string jsonData);
    }
}