namespace ZES.Interfaces.Net
{
    /// <summary>
    /// JSON result interface
    /// </summary>
    public interface IJsonResult
    {
        /// <summary>
        /// Gets or sets request correlation id
        /// </summary>
        string RequestorId { get; set; }
    }
}