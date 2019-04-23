using System;
using System.Linq.Expressions;
using SimpleInjector;

namespace ZES
{
    /// <summary>
    /// getUtcNow argument convention for <see cref="NLog"/>, injects null
    /// </summary>
    public class UtcNowConvention : BaseParameterConvention
    {
        /// <inheritdoc />
        public override bool CanResolve(InjectionTargetInfo target) => target.TargetType == typeof(DateTime) && target.Name == "getUtcNow";

        /// <inheritdoc />
        public override Expression BuildExpression(InjectionConsumerInfo consumer)
        {
            return Expression.Constant(null, typeof(DateTime));
        }
    } 
}