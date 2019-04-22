using System;
using System.Linq;

namespace ZES
{
    public class TypeProvider<TType> : ITypeProvider<TType>
        where TType : class
    {
        public TypeProvider(Type clrType)
        {
            if (clrType.GetInterfaces().Contains(typeof(TType)))
                ClrType = clrType;
        }

        public Type ClrType { get; }
    }
}