using ZES.Infrastructure.Attributes;
using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    // [Idempotent]
    public class UpdateRoot : Command   
    {
        public UpdateRoot() { }

        public UpdateRoot(string target, long timestamp = default(long))
            : base(target)
        {
            Timestamp = timestamp;
        }
    }
}