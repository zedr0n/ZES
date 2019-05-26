using ZES.Infrastructure.Domain;
using ZES.Interfaces;

namespace ZES.Infrastructure.Alerts
{
    /// <inheritdoc cref="IAlert" />
    public class Alert : Message, IAlert
    {
    }
}