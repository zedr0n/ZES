using System.Threading;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class Tracked<T> : TrackedResult<T, bool>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Tracked{T}"/> class.
        /// </summary>
        /// <param name="value">Tracked value</param>
        /// <param name="token">Cancellation token</param>
        public Tracked(T value, CancellationToken token = default)
            : base(value, token)
        {
        }
    }
}