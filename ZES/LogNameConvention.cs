using System.Linq.Expressions;
using SimpleInjector;

namespace ZES
{
    public class LogNameConvention : BaseParameterConvention
    {
        public override bool CanResolve(InjectionTargetInfo target) => target.TargetType == typeof(string) && target.Name == "logName";

        public override Expression BuildExpression(InjectionConsumerInfo consumer)
        {
            return Expression.Constant(null, typeof(string));
        }
    }
}