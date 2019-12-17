using System.Threading;

namespace ZES.Tests.Domain.Queries
{
    public class Stats
    {
        private int _numberOfRoots;

        public int NumberOfRoots => _numberOfRoots;

        public void Increment()
        {
            Interlocked.Increment(ref _numberOfRoots);
        }
    }
}