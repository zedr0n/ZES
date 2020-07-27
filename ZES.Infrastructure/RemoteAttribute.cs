using System;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Defines remote store attribute 
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RemoteAttribute : Attribute { }
}