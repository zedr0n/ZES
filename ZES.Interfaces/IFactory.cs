namespace ZES.Interfaces
{
    /// <summary>
    /// Service factory interface
    /// </summary>
    /// <typeparam name="T">Underlying service type</typeparam>
    public interface IFactory<out T>
        where T : class
    {
        /// <summary>
        /// Create a transient instance of the service
        /// </summary>
        /// <returns>Service instance</returns>
        T Create();
    }
}