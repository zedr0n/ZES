using System;

namespace ZES.Infrastructure.Attributes
{
    /// <summary>
    /// Defines remote store attribute 
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RemoteAttribute : Attribute { }
}