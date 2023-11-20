using ZES.Infrastructure.Domain;

namespace ZES.Tests.Domain.Sagas
{
    public class NewRootSagaEvent : Event
    {
        public string RootId { get; set; }
    }
}