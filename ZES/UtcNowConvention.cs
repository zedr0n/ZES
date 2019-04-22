using System;
using System.Linq.Expressions;
using SimpleInjector;

namespace ZES
{
    public class UtcNowConvention : BaseParameterConvention
    {
        public override bool CanResolve(InjectionTargetInfo target) => target.TargetType == typeof(DateTime) && target.Name == "getUtcNow";

        public override Expression BuildExpression(InjectionConsumerInfo consumer)
        {
            return Expression.Constant(null, typeof(DateTime));
        }
    } 
}