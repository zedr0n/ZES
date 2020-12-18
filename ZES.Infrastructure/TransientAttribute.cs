using System;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Transient attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TransientAttribute : Attribute { }
}