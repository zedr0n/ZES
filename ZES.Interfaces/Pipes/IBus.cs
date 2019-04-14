using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Pipes
{
    public interface IBus
    {
        Task<Task> CommandAsync(ICommand command);

        Task<TResult> QueryAsync<TResult>(IQuery<TResult> query);
    }
}