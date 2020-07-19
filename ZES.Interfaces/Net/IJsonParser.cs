namespace ZES.Interfaces.Net
{
    /// <summary>
    /// JSON parser service
    /// </summary>
    public interface IJsonParser
    {
        /// <summary>
        /// Parse json string
        /// </summary>
        /// <param name="json">JSON payload</param>
        /// <typeparam name="T">Result type</typeparam>
        /// <returns>Parsed JSON result</returns>
        T Parse<T>(string json);
    }
}