using System.Threading.Tasks;

namespace ZES.Interfaces.Domain
{
    public interface IProjection
    {
        Task Complete { get; }
    }
    
    public interface IProjection<out TState> : IProjection
    {
        TState State { get; }
    }
}