using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Snapshot command
    /// </summary>
    /// <typeparam name="TRoot">Target aggregate</typeparam>
    public class CreateSnapshot<TRoot> : Command
        where TRoot : class, IAggregate, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CreateSnapshot{TRoot}"/> class.
        /// </summary>
        /// <param name="target">Aggregate id</param>
        public CreateSnapshot(string target)
            : base(target)
        {
        }
    }
}