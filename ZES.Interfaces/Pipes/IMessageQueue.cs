using System;

namespace ZES.Interfaces.Pipes
{
    public interface IMessageQueue
    {
        IObservable<IEvent> Messages { get; }
        IObservable<IAlert> Alerts { get; }
        void Event(IEvent e);
        void Alert(IAlert alert);
    }
}