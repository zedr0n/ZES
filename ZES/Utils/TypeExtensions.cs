using System;
using System.Linq;

namespace ZES.Utils
{
    /// <summary>
    /// Type extensions
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Gets properly expanded template name for the type
        /// </summary>
        /// <param name="type">Input type</param>
        /// <returns>Name string</returns>
        public static string GetFriendlyName(this Type type)
        {
            if (!type.IsGenericType)
                return type.Name;
            return
                $"{type.Name.Split('`')[0]}<{string.Join(", ", type.GetGenericArguments().Select(Gridsum.DataflowEx.Utils.GetFriendlyName))}>";
        }
    }
}