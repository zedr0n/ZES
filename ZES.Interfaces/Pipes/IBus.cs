using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Interfaces.Pipes
{
    public interface IBus
    {
        Task<bool> CommandAsync(ICommand command);

        Task<TResult> QueryAsync<TResult>(IQuery<TResult> query);
    }
}