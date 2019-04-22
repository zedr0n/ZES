using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    public class Command : ICommand
    {
        protected Command() { }

        protected Command(string target)
        {
            Target = target;
        }

        public string Target { get; set; }

        public long? Timestamp { get; set; }
    }
}