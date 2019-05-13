using System;

namespace ZES.Infrastructure.Utils
{
    /// <summary>
    /// Fluent builder
    /// </summary>
    public class FluentBuilder
    {
        /// <summary>
        /// Clones builder
        /// </summary>
        /// <param name="builder">Builder</param>
        /// <param name="setter">Property to set</param>
        /// <typeparam name="T">Builder runtime type</typeparam>
        /// <returns>Cloned builder</returns>
        protected static T Clone<T>(T builder, Action<T> setter)
            where T : FluentBuilder
        {
            var b = (T)builder.MemberwiseClone();
            setter(b);
            return b;
        } 
    }
}