using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class Query<T> : IQuery<T>
    {
        /// <inheritdoc />
        public string Timeline { get; set; } = string.Empty;
    }
}