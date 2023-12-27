using System;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Represents a Transient attribute.
    /// This attribute is used to mark a class as transient.
    /// Transient classes are not persisted in the storage.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TransientAttribute : Attribute { }
}