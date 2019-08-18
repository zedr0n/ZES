using System.Threading.Tasks;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Track the underlying object with task completion
    /// </summary>
    /// <typeparam name="T">Underlying type</typeparam>
    /// <typeparam name="TResult">Tracking result type</typeparam>
    public class Tracked<T, TResult> 
    {
        private readonly TaskCompletionSource<TResult> _tsc;

        /// <summary>
        /// Initializes a new instance of the <see cref="Tracked{T, TResult}"/> class.
        /// </summary>
        /// <param name="value">Underlying value</param>
        public Tracked(T value)
        {
            _tsc = new TaskCompletionSource<TResult>();
            Value = value;
        }
        
        /// <summary>
        /// Gets wrapped value 
        /// </summary>
        public T Value { get; }
        
        /// <summary>
        /// Gets completion task
        /// </summary>
        public Task<TResult> Task => _tsc.Task;
        
        /// <summary>
        /// Set completion result
        /// </summary>
        /// <param name="result">Completion result</param>
        public void SetResult(TResult result) { _tsc.SetResult(result); }
    }

    /// <inheritdoc />
    public class Tracked<T> : Tracked<T, bool>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Tracked{T}"/> class.
        /// </summary>
        /// <param name="value">Tracked value</param>
        public Tracked(T value)
            : base(value)
        {
        }

        /// <summary>
        /// Set completion
        /// </summary>
        public void Complete() => SetResult(true);
    }
}