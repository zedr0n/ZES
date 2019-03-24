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
        bool Command(ICommand command);
        Task<bool> CommandAsync(ICommand command);

        TResult Query<TResult>(IQuery<TResult> query);
        Task<TResult> QueryAsync<TResult>(IQuery<TResult> query);
    }
}