using System;

namespace ZES.Infrastructure.Replicas
{
    /// <summary>
    /// Defines remote store attribute 
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RemoteAttribute : Attribute { }
}