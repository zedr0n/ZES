using System;
using System.Linq.Expressions;
using SimpleInjector;

namespace ZES.Conventions
{
    /// <inheritdoc />
    public abstract class BaseParameterConvention : IParameterConvention
    {
        private Type Consumer { get; set; }

        /// <inheritdoc />
        public bool Handles(InjectionConsumerInfo consumer)
        {
            if (Consumer == null)
                return true;
            return Consumer.IsAssignableFrom(consumer.ImplementationType);
        }

        /// <inheritdoc />
        public abstract bool CanResolve(InjectionTargetInfo target);

        /// <inheritdoc />
        public abstract Expression BuildExpression(InjectionConsumerInfo consumer);
    }
}