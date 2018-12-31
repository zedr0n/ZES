namespace ZES.Interfaces.Domain
{
    public interface ICommand
    {
        /// <summary>
        /// Aggregate target id
        /// </summary>
        string AggregateId { get; set; }
        /// <summary>
        /// Unix time offset for command timestamp
        /// </summary>
        long Timestamp { get; set; }
    }
}