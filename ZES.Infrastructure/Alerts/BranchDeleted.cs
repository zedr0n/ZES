namespace ZES.Infrastructure.Alerts
{
    /// <summary>
    /// Branch deleted alert
    /// </summary>
    public class BranchDeleted : Alert
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BranchDeleted"/> class.
        /// </summary>
        /// <param name="branchId">Branch id</param>
        public BranchDeleted(string branchId)
        {
            BranchId = branchId;
        }

        /// <summary>
        /// Gets branch id
        /// </summary>
        public string BranchId { get; }
    }
}