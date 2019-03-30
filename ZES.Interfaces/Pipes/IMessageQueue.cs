using System;
using System.Threading.Tasks;

namespace ZES.Interfaces.Pipes
{
    public interface IMessageQueue
    {
        IObservable<IEvent> Messages { get; }
        IObservable<IAlert> Alerts { get; }
        Task Event(IEvent e);
        Task Alert(IAlert alert);
    }
}