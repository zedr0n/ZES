using System;
using System.Linq.Expressions;
using SimpleInjector;

namespace ZES
{
    public abstract class BaseParameterConvention : IParameterConvention
    {
        public Type Consumer { get; set; }

        public bool Handles(InjectionConsumerInfo consumer)
        {
            if (Consumer == null)
                return true;
            return Consumer.IsAssignableFrom(consumer.ImplementationType);
        }

        public abstract bool CanResolve(InjectionTargetInfo target);
        public abstract Expression BuildExpression(InjectionConsumerInfo consumer);
    }
}