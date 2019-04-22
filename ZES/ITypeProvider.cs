using System;

namespace ZES
{
    public interface ITypeProvider<TType>
        where TType : class
    {
        Type ClrType { get; }
    }
}