using ZES.Infrastructure.Domain;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class ReplaceRoot    
    {
        public ReplaceRoot() { };
        public ReplaceRoot(string target) 
            : base(target) { }
    }
}