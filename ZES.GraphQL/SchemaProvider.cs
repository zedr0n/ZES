using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Stitching;
using HotChocolate.Subscriptions;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;
using ZES.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class SchemaProvider : ISchemaProvider
    {
        private readonly IBus _bus;
        private readonly ILog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaProvider"/> class.
        /// </summary>
        /// <param name="bus">Message bus</param>
        /// <param name="log">Application error log</param>
        public SchemaProvider(IBus bus, ILog log)
        {
            _bus = bus;
            _log = log;
        }

        /// <inheritdoc />
        public IServiceCollection Register(IServiceCollection services, IEnumerable<Type> rootQuery, IEnumerable<Type> rootMutation)
        {
            if (services == null)
                services = new ServiceCollection();
            
            services.AddSingleton(typeof(ILog), _log);
            services.AddSingleton(typeof(IBus), _bus);

            services.AddInMemorySubscriptionProvider();
            
            var baseSchema = Schema.Create(c =>
            {
                c.RegisterExtendedScalarTypes();
                
                // c.Use(Middleware());
                c.RegisterQueryType(typeof(BaseQuery));
                c.RegisterSubscriptionType<SubscriptionType>();
            });
            
            var domainSchemas = rootQuery.Zip(rootMutation, (a, b) =>
                                                                (a, b))
                .Select(t => Schema.Create(c =>            
            {
                c.RegisterExtendedScalarTypes();
                
                // c.Use(Middleware(t.Item1, t.Item2));
                if (t.Item1 != null)
                    c.RegisterQueryType(typeof(ObjectType<>).MakeGenericType(t.Item1));

                if (t.Item2 == null) 
                    return;
                
                foreach (var p in t.Item2.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                     BindingFlags.DeclaredOnly))
                {
                    var arg = p.GetParameters().FirstOrDefault()?.ParameterType;
                    if (arg != null && arg.GetInterfaces().Contains(typeof(ICommand)))
                        c.RegisterType(typeof(CommandType<>).MakeGenericType(arg));
                }
                c.RegisterMutationType(typeof(ObjectType<>).MakeGenericType(t.Item2));
            })).ToList();

            void AggregateSchemas(IStitchingBuilder b)
            {
                b.AddSchema("Base", baseSchema);
                
                var i = 0;
                foreach (var s in domainSchemas)
                {
                    b = b.AddSchema($"Domain{i}", s);
                    i++;
                }
            }
           
            services.AddStitchedSchema(AggregateSchemas);
            
            return services;
        }

        /// <inheritdoc />
        public IQueryExecutor Generate(Type rootQuery = null, Type rootMutation = null)
        {
            var services = Register(null, rootQuery, rootMutation);

            var provider = services.BuildServiceProvider();
            
            /*var baseSchema = Schema.Create(c =>
            {
                c.RegisterServiceProvider(provider);
                
                c.RegisterExtendedScalarTypes();
                // c.Use(Middleware());
                c.RegisterQueryType(typeof(BaseQuery));
                c.RegisterSubscriptionType<SubscriptionType>();
            });

            var executor = baseSchema.MakeExecutable();
            var eventRegistry = provider.GetService<IEventRegistry>();
 
            */
            
            var executor = provider.GetService<IQueryExecutor>();
            var sender = provider.GetService<IEventSender>();

            _log.Logs.Subscribe(async m => await sender.SendAsync(
                new OnLogMessage(new LogMessage(m))));

            return executor;
        }
        
        private IServiceCollection Register(IServiceCollection services, Type rootQuery, Type rootMutation)
        {
            return Register(services, new[] { rootQuery }, new[] { rootMutation });
        }
    }
}