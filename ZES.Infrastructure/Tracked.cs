namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class Tracked<T> : TrackedResult<T, bool>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Tracked{T}"/> class.
        /// </summary>
        /// <param name="value">Tracked value</param>
        public Tracked(T value)
            : base(value)
        {
        }
    }
}