using System;
using System.Linq.Expressions;
using System.Reflection;
using SimpleInjector;

namespace ZES.Utils
{
    /// <summary>
    /// Automatic IFactory interface implementation
    /// </summary>
    public static class AutomaticFactoryExtensions 
    {
        /// <summary>
        /// SimpleInjector extension to register singleton factories for services
        /// </summary>
        /// <param name="container">SimpleInjector container</param>
        /// <param name="factoryType">IFactory type</param>
        /// <exception cref="ArgumentException">Throws if wrapped service type is not an interface</exception>
        public static void RegisterFactory(this Container container, Type factoryType) 
        {
            if (!factoryType.IsInterface)
                throw new ArgumentException(factoryType.Name + " is no interface");

            container.ResolveUnregisteredType += (s, e) => 
            {
                if (e.UnregisteredServiceType == factoryType)
                {
                    var t = typeof(AutomaticFactoryProxy<>).MakeGenericType(factoryType);
                    var proxy = t.InvokeMember(
                        nameof(AutomaticFactoryProxy<object>.CreateFactory), 
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod,
                        null, 
                        null, 
                        new object[] { container });
                    e.Register(Expression.Constant(proxy, factoryType));
                }
            };
        }

        /// <inheritdoc />
        public class AutomaticFactoryProxy<TFactory> : DispatchProxy 
            where TFactory : class
        {
            private Container _container;

            /// <summary>
            /// Creates the factory instance
            /// </summary>
            /// <param name="container">SimpleInjector container</param>
            /// <returns>Proxy instance</returns>
            public static TFactory CreateFactory(Container container)
            {
                var proxy = Create<TFactory, AutomaticFactoryProxy<TFactory>>() as AutomaticFactoryProxy<TFactory>;
                if (proxy == null) 
                    return null;
                
                proxy._container = container;
                return proxy as TFactory;
            }

            /// <inheritdoc />
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                switch (targetMethod.Name)
                {
                    case "GetType":
                        return typeof(TFactory);
                    case "ToString":
                        return typeof(TFactory).Name;
                    default:
                    {
                        var instance = _container.GetInstance(targetMethod.ReturnType);
                        return instance;
                    }
                }
            }
        }
    }
}