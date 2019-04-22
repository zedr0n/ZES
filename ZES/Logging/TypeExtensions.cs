using System;
using System.Linq;

namespace ZES.Logging
{
    public static class TypeExtensions
    {
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