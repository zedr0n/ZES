using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Stitching;
using HotChocolate.Stitching.Merge;
using HotChocolate.Types;
using HotChocolate.Utilities;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using ZES.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.GraphQL
{
    public static class Startup
    {
        public static void WireGraphQL(this IServiceCollection services, Container container, Action<Container> config,
            Type rootQuery, Type rootMutation)
        {
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            new CompositionRoot().ComposeApplication(container);
            container.Register<ISchemaProvider,SchemaProvider>(Lifestyle.Singleton);
            config(container);
            
            container.Verify(); 
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            schemaProvider.Register(services, rootQuery, rootMutation);
        }
        
        
        public static IQueryExecutor WireGraphQL(Container container, Action<Container> config,
            Type rootQuery, Type rootMutation)
        {
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            new CompositionRoot().ComposeApplication(container);
            container.Register<ISchemaProvider,SchemaProvider>(Lifestyle.Singleton);
            config(container);
            
            container.Verify();

            var schemaProvider = container.GetInstance<ISchemaProvider>();
            var executor = schemaProvider.Generate(rootQuery, rootMutation);
            
            return executor;
        }
    }
    
    public class SchemaProvider : ISchemaProvider
    {
        private readonly IBus _bus;
        private readonly IErrorLog _errorLog;

        public SchemaProvider(IBus bus, IErrorLog errorLog)
        {
            _bus = bus;
            _errorLog = errorLog;
        }

        private FieldMiddleware Middleware(Type rootQuery = null, Type rootMutation = null)
        {
            return next => async context =>
            {
                if (string.Compare(context.Field.Name, nameof(ErrorLog.Error), StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var error = await _errorLog.Errors.FirstAsync();
                    context.Result = error;
                    return;
                }

                var fields =
                    rootQuery?.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                var field = fields?.SingleOrDefault(x =>
                    string.Compare(x.Name, context.Field.Name, StringComparison.OrdinalIgnoreCase) == 0);
                if (field != null)
                {
                    var queryType = field.GetParameters().FirstOrDefault();
                    dynamic query = context.Argument<object>(queryType.Name);
                    context.Result = await _bus.QueryAsync(query);
                    return;
                }

                var commandFields =
                    rootMutation?.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                field = commandFields?.SingleOrDefault(x =>
                    string.Compare(x.Name, context.Field.Name, StringComparison.OrdinalIgnoreCase) == 0);

                if (field != null)
                {
                    var commandType = field.GetParameters().FirstOrDefault();
                    dynamic command = context.Argument<object>(commandType.Name);
                    var isError = false;
                    _errorLog.Errors.Subscribe(e =>
                    {
                        if (e.ErrorType == typeof(InvalidOperationException).Name)
                            isError = true;
                    });
                    await await _bus.CommandAsync(command);
                    context.Result = !isError;
                    return;
                }

                await next.Invoke(context);
            };
        }

        public IServiceCollection Register(IServiceCollection services, Type rootQuery, Type rootMutation)
        {
            var baseSchema = Schema.Create(c =>
            {
                c.RegisterExtendedScalarTypes();
                c.Use(Middleware());
                c.RegisterQueryType(typeof(BaseQuery));
            });
            
            var domainSchema = Schema.Create(c =>
            {
                c.RegisterExtendedScalarTypes();
                c.Use(Middleware(rootQuery, rootMutation));
                if (rootQuery != null)
                    c.RegisterQueryType(typeof(ObjectType<>).MakeGenericType(rootQuery)); 
                
                if(rootMutation != null)
                    c.RegisterMutationType(typeof(ObjectType<>).MakeGenericType(rootMutation)); 
            });
           
            if(services == null)
                services = new ServiceCollection();
            services.AddStitchedSchema(builder => builder.AddSchema("Base", baseSchema)
                .AddSchema("Domain", domainSchema));

            return services;
        }
        
        public IQueryExecutor Generate(Type rootQuery = null, Type rootMutation = null)
        {
            var services = Register(null, rootQuery, rootMutation);
                    
            var executor = services.BuildServiceProvider().GetService<IQueryExecutor>();
            return executor;
        }
    }
}