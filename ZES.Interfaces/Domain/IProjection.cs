using System;
using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    public interface IProjection
    {
        void Start(bool rebuild = true);
        IObservable<bool> Complete { get; }
    }
    public interface IProjection<TState> : IProjection
    {
        TState State { get; }
    }
}