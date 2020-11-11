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
        {
            SnapshotRootId = target;
        }
        
        /// <summary>
        /// Gets or sets the snapshot root id
        /// </summary>
        public string SnapshotRootId { get; set; }

        /// <inheritdoc />
        public override string Target => SnapshotRootId;
    }
}