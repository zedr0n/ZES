using System;
using System.Linq.Expressions;
using SimpleInjector;

namespace ZES.CrossCuttingConcerns
{
    public class LogNameConvention : BaseParameterConvention
    {
        public override bool CanResolve(InjectionTargetInfo target) => target.TargetType == typeof(string) && target.Name == "logName";

        public override Expression BuildExpression(InjectionConsumerInfo consumer)
        {
            return Expression.Constant(null, typeof(string));
        }
    }
    
    public class UtcNowConvention : BaseParameterConvention
    {
        public override bool CanResolve(InjectionTargetInfo target) => target.TargetType == typeof(DateTime) && target.Name == "getUtcNow";

        public override Expression BuildExpression(InjectionConsumerInfo consumer)
        {
            return Expression.Constant(null, typeof(DateTime));
        }
    } 
    
}