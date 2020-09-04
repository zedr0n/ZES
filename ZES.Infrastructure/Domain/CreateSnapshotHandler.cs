using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public class CreateSnapshotHandler<TRoot> : CommandHandlerBase<CreateSnapshot<TRoot>, TRoot>
        where TRoot : class, IAggregate, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CreateSnapshotHandler{TRoot}"/> class.
        /// </summary>
        /// <param name="repository">ES repository</param>
        public CreateSnapshotHandler(IEsRepository<IAggregate> repository)
            : base(repository)
        {
            ComputeHash = true;
        }

        /// <inheritdoc />
        protected override void Act(TRoot root, CreateSnapshot<TRoot> command) => root.Snapshot();
    }
}