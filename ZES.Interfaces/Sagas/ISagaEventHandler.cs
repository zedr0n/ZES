namespace ZES.Interfaces.Sagas
{
    public interface ISagaEventHandler
    {
        void Handle<TEvent, T>(T saga, TEvent e) where TEvent : class, IEvent
            where T : class, ISaga;
    }
}