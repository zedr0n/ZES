using System;
using System.Collections.Generic;
using System.Linq;
using Gridsum.DataflowEx;

namespace ZES.Logging
{
    internal static class TypeExtensions
    {

        public static string GetFriendlyName(this Type type)
        {
            if (!type.IsGenericType)
                return type.Name;
            return
                $"{type.Name.Split('`')[0]}<{string.Join(", ", type.GetGenericArguments().Select(Utils.GetFriendlyName))}>";
        }
    }
}