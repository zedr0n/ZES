using System;
using System.Linq;

namespace ZES
{
    public interface ITypeProvider<TType> where TType : class
    {
        Type ClrType { get; }
    }
    
    public class TypeProvider<TType> : ITypeProvider<TType> where TType : class
    {
        public Type ClrType { get; }

        public TypeProvider(Type clrType)
        {
            if(clrType.GetInterfaces().Contains(typeof(TType)))
                ClrType = clrType;
        }
    }
}