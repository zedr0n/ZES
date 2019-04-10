using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Pipes
{
    public enum BusStatus
    {
        Free,
        Submitted,
        Executing,
        Busy
    }
    
    public interface IBus
    {
        BusStatus Status { get; }
        Task<bool> CommandAsync(ICommand command);

        Task<TResult> QueryAsync<TResult>(IQuery<TResult> query);
    }
}