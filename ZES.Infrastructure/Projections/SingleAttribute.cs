using System;

namespace ZES.Infrastructure.Projections
{
    /// <summary>
    /// One to many projection
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SingleAttribute : Attribute { }
}