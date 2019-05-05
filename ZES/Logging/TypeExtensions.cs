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
        
        public static string GetName(this Type type)
        {
            var genericArguments = type.GetGenericArguments();
            var str = type.Name.Split('`')[0];
            if (genericArguments.Length > 0)
                str = $"{str}<";
            foreach (var arg in genericArguments)
            {
                str = $"{str}{arg.GetName()}";
                if (arg != genericArguments.Last())
                    str = $"{str},";
            }

            if (genericArguments.Length > 0)
                str = $"{str}>";
            return str;
        }
    }
}