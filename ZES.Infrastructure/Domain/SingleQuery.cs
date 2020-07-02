using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Single stream query
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    public class SingleQuery<T> : Query<T>, ISingleQuery<T>
        where T : ISingleState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SingleQuery{T}"/> class.
        /// </summary>
        public SingleQuery()
        {
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="SingleQuery{T}"/> class.
        /// </summary>
        /// <param name="id">Id</param>
        public SingleQuery(string id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets or sets the id
        /// </summary>
        public string Id { get; set; }
    }
}