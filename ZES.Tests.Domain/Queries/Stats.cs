using System.Threading;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Queries
{
    public class Stats : IState
    {
        private int _numberOfRoots;

        public int NumberOfRoots
        {
            get => _numberOfRoots;
            set => _numberOfRoots = value;
        }

        public void Increment()
        {
            Interlocked.Increment(ref _numberOfRoots);
        }
    }
}