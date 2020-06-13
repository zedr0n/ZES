using System.Threading;
using System.Threading.Tasks;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Track the underlying object with task completion
    /// </summary>
    /// <typeparam name="T">Underlying type</typeparam>
    /// <typeparam name="TResult">Tracking result type</typeparam>
    public class TrackedResult<T, TResult> : ITracked 
    {
        private readonly TaskCompletionSource<TResult> _tsc;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrackedResult{T,TResult}"/> class.
        /// </summary>
        /// <param name="value">Underlying value</param>
        /// <param name="token">Cancellation token</param>
        public TrackedResult(T value, CancellationToken token = default)
        {
            _tsc = new TaskCompletionSource<TResult>();
            Value = value;
            token.Register(Complete);
        }

        /// <summary>
        /// Gets wrapped value 
        /// </summary>
        public T Value { get; }
        
        /// <summary>
        /// Gets completion task
        /// </summary>
        public Task<TResult> Task => _tsc.Task;
        
        /// <inheritdoc />
        public Task Completed => Task;
        
        /// <summary>
        /// Set completion result
        /// </summary>
        /// <param name="result">Completion result</param>
        public void SetResult(TResult result) { _tsc.SetResult(result); }

        /// <inheritdoc />
        public void Complete() => _tsc.TrySetResult(default(TResult));
    }
}