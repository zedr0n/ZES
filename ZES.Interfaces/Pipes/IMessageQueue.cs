using System;
using System.Threading.Tasks;

namespace ZES.Interfaces.Pipes
{
    public interface IMessageQueue
    {
        IObservable<IEvent> Messages { get; }
        IObservable<string> Alerts { get; }
        Task PublishAsync(IEvent e);
        Task PublishAlert(string alert);
    }
}