using System;
using System.Threading.Tasks;

namespace ZES.Interfaces.Pipes
{
    public interface IMessageQueue
    {
        IObservable<IEvent> Messages { get; }
        Task PublishAsync(IEvent e);
    }
}