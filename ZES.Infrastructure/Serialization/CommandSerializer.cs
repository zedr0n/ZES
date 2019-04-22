using ZES.Interfaces.Domain;
using ZES.Interfaces.Serialization;

namespace ZES.Infrastructure.Serialization
{
    public class CommandSerializer : Serializer<ICommand>, ICommandSerializer { }
}