using System;

namespace ZES.Interfaces.Domain
{
    public interface IProjection
    {
        IObservable<bool> Complete { get; }
    }
    public interface IProjection<TState> : IProjection
    {
        TState State { get; }
    }
}