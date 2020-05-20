using System.Threading;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class Stats : IState
    {
        private int _numberOfRoots;

        public int NumberOfRoots => _numberOfRoots;

        public void Increment()
        {
            Interlocked.Increment(ref _numberOfRoots);
        }
    }
}