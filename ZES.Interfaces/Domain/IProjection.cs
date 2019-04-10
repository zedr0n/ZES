using System;
using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    public interface IProjection
    {
        Task Start(bool rebuild = true);
        Task Complete { get; }
    }
    public interface IProjection<TState> : IProjection
    {
        TState State { get; }
    }
}