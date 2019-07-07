using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using ZES.Infrastructure.Attributes;

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
        public static void WireGraphQl(this IServiceCollection services, Type config)
        {
            var container = new Container();
            container.Options.DefaultLifestyle = Lifestyle.Singleton;

            WireGraphQl(services, container, new[] { config });
        }

        /// <summary>
        /// Wires the root queries and mutations for multiple domains defined by their respective config types 
        /// </summary>
        /// <param name="services"><see cref="IServiceCollection"/></param>
        /// <param name="container"><see cref="SimpleInjector"/> container</param>
        /// <param name="configs">Config types containing the domain registration, root queries and mutations</param>
        public static void WireGraphQl(this IServiceCollection services, Container container, IEnumerable<Type> configs)
        {
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            new CompositionRoot().ComposeApplication(container);
            container.Register<IServiceCollection>(() => services, Lifestyle.Singleton);
            container.Register<ISchemaProvider, SchemaProvider>(Lifestyle.Singleton);

            var config = Logging.NLog.Configure();
            Logging.NLog.Enable(config);
            
            foreach (var t in configs)
            {
                var regMethod = t.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .SingleOrDefault(x => x.GetCustomAttribute<RegistrationAttribute>() != null);

                regMethod?.Invoke(null, new object[] { container });
            }
            
            container.Verify();
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            // schemaProvider.Services();
        }
    }
}