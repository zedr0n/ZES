using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Serialization
{
    public interface IEventSerializer : ISerializer<IEvent> { }
    public interface ICommandSerializer : ISerializer<ICommand> { }
}