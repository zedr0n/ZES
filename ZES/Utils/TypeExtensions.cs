using System;
using System.Linq;

namespace ZES.Utils
{
    internal static class TypeExtensions
    {
        public static string GetFriendlyName(this Type type)
        {
            if (!type.IsGenericType)
                return type.Name;
            return
                $"{type.Name.Split('`')[0]}<{string.Join(", ", type.GetGenericArguments().Select(Gridsum.DataflowEx.Utils.GetFriendlyName))}>";
        }
    }
}