using System.Linq.Expressions;
using SimpleInjector;

namespace ZES.Conventions
{
    /// <summary>
    /// Responsible for injecting <c>null</c> for <c>logName</c> argument for NLogger
    /// </summary>
    public class LogNameConvention : BaseParameterConvention
    {
        /// <inheritdoc />
        public override bool CanResolve(InjectionTargetInfo target) => target.TargetType == typeof(string) && target.Name == "logName";

        /// <inheritdoc />
        public override Expression BuildExpression(InjectionConsumerInfo consumer)
        {
            return Expression.Constant(null, typeof(string));
        }
    }
}