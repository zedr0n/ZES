using System.Threading.Tasks;

namespace ZES.Infrastructure.Projections
{
    public interface IHistoricalProjection
    {
        Task Init(long timestamp);
    }
}