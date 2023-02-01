using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using ZES.Infrastructure;

namespace ZES.GraphQL
{
    /// <summary>
    /// AspNet.Core GraphQL wiring for <see cref="SimpleInjector"/>
    /// </summary>
    public static class GraphQlExtensions
    {
        /// <summary>
        /// Wires the root queries and mutations for a single config type
        /// </summary>
        /// <param name="services"><see cref="IServiceCollection"/></param>
        /// <param name="config">Config type containing the domain registration, root queries and mutations</param>
        /// <param name="logger">Logger instance ( for XUnit )</param>
        /// <param name="useRemoteStore">Use remote store</param>
        public static void UseGraphQl(this IServiceCollection services, Type config, ILogger logger = null, bool useRemoteStore = false)
        {
            var container = new Container();
            container.Options.DefaultLifestyle = Lifestyle.Singleton;

            // services.AddInMemorySubscriptionProvider();
            UseGraphQl(services, container, new[] { config }, logger);
        }

        /// <summary>
        /// Wires the root queries and mutations for multiple config types
        /// </summary>
        /// <param name="services"><see cref="IServiceCollection"/>></param>
        /// <param name="configs">Root configs</param>
        /// <param name="logger">Logger instance ( for XUnit )</param>
        /// <param name="useRemoteStore">Use remote store</param>
        public static void UseGraphQl(this IServiceCollection services, IEnumerable<Type> configs, ILogger logger = null, bool useRemoteStore = false)
        {
            var container = new Container();
            container.Options.DefaultLifestyle = Lifestyle.Singleton;

            // services.AddInMemorySubscriptionProvider();
            UseGraphQl(services, container, configs, logger, useRemoteStore);
        }

        /// <summary>
        /// Wires the root queries and mutations for multiple domains defined by their respective config types 
        /// </summary>
        /// <param name="services"><see cref="IServiceCollection"/></param>
        /// <param name="container"><see cref="SimpleInjector"/> container</param>
        /// <param name="configs">Config types containing the domain registration, root queries and mutations</param>
        /// <param name="logger">Logger instance ( for XUnit )</param>
        /// <param name="useRemoteStore">Use remote store</param>
        private static void UseGraphQl(this IServiceCollection services, Container container, IEnumerable<Type> configs, ILogger logger = null, bool useRemoteStore = false)
        {
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            var root = new CompositionRoot();
            root.ComposeApplication(container);
            container.Register(() => services, Lifestyle.Singleton);
            container.Register<ISchemaProvider, SchemaProvider>(Lifestyle.Singleton);
            if (useRemoteStore)
                root.RegisterRemoteStore(container, false);

            if (logger != null)
            {
                container.Options.AllowOverridingRegistrations = true;
                container.Register(typeof(ILogger), () => logger, Lifestyle.Singleton);
                container.Options.AllowOverridingRegistrations = false;
            }
            else
            {
                var config = Logging.NLog.Configure();
                Logging.NLog.Enable(config);
            }

            foreach (var t in configs)
            {
                var regMethod = t.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .SingleOrDefault(x => x.GetCustomAttribute<RegistrationAttribute>() != null);

                regMethod?.Invoke(null, new object[] { container });
            }
            
            // container.Verify();
            root.Verify(container);

            services.AddSingleton(typeof(Container), t => container);
        }
    }
}