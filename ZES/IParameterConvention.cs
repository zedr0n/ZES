using System;
using System.Linq.Expressions;
using SimpleInjector;

namespace ZES
{
    /// <summary>
    /// Parameter convention to inject scalar types
    /// </summary>
    public interface IParameterConvention
    {
        /// <summary>
        /// Defines if convention is applicable to consumer type 
        /// </summary>
        /// <param name="consumer"><see cref="SimpleInjector"/> consumer info</param>
        /// <returns>True if convention is applicable for consumer</returns>
        bool Handles(InjectionConsumerInfo consumer);
        
        /// <summary>
        /// Defines if the convention can be used for injection target
        /// </summary>
        /// <param name="target"><see cref="InjectionTargetInfo"/></param>
        /// <returns>True if convention is applicable for target argument</returns>
        bool CanResolve(InjectionTargetInfo target);
        
        /// <summary>
        /// Delegate for expression to inject the argument 
        /// </summary>
        /// <param name="consumer"><see cref="SimpleInjector"/> consumer info</param>
        /// <returns>Expression to inject</returns>
        Expression BuildExpression(InjectionConsumerInfo consumer);
    }
}