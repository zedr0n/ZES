namespace ZES.Interfaces
{
    public interface ILog 
    {
        /// <summary>
        /// Writes the diagnostic message at the <c>Trace</c> level.
        /// </summary>
        /// <param name="value">A <see langword="object" /> to be written.</param>
        void Trace(object value); 
        
        /// <summary>
        /// Writes the diagnostic message at the <c>Debug</c> level.
        /// </summary>
        /// <param name="value">A <see langword="object" /> to be written.</param>
        void Debug(object value);  
        
        /// <summary>
        /// Writes the diagnostic message at the <c>Error</c> level.
        /// </summary>
        /// <param name="value">A <see langword="object" /> to be written.</param>
        void Error(object value);  
        
        /// <summary>
        /// Writes the diagnostic message at the <c>Fatal</c> level.
        /// </summary>
        /// <param name="value">A <see langword="object" /> to be written.</param>
        void Fatal(object value);  
    }
}