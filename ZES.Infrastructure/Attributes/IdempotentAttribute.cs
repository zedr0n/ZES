using System;

namespace ZES.Infrastructure.Attributes
{
    /// <summary>
    /// Commands marked with attribute are idempotent
    /// </summary>
    public class IdempotentAttribute : Attribute
    {
    }
}