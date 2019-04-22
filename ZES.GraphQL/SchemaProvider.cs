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
using ZES.Infrastructure.Attributes;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.GraphQL
{
    public static class Startup
    {
        public static void WireGraphQl(this IServiceCollection services, Type config)
        {
            var container = new Container();
            WireGraphQl(services, container, new[] {config});
        }
        public static void WireGraphQl(this IServiceCollection services, Container container, IEnumerable<Type> configs)
        {
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            new CompositionRoot().ComposeApplication(container);
            container.Register<ISchemaProvider,SchemaProvider>(Lifestyle.Singleton);
            
            // load root queries and mutations
            var rootQueries = new List<Type>();
            var rootMutations = new List<Type>();
            foreach (var t in configs)
            {
                rootQueries.Add(t.GetNestedTypes()
                    .SingleOrDefault(x => x.GetCustomAttribute<RootQueryAttribute>() != null));
                rootMutations.Add(t.GetNestedTypes()
                    .SingleOrDefault(x => x.GetCustomAttribute<RootMutationAttribute>() != null));

                var regMethod = t.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .SingleOrDefault(x => x.GetCustomAttribute<RegistrationAttribute>() != null);

                regMethod?.Invoke(null, new object[] {container});
            }
            
            container.Verify();
            
            var schemaProvider = container.GetInstance<ISchemaProvider>();
            schemaProvider.Register(services, rootQueries.ToArray(), rootMutations.ToArray()); 
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

        private FieldMiddleware Middleware(IReflect rootQuery = null, IReflect rootMutation = null)
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
            return Register(services, new[] {rootQuery}, new[] {rootMutation});
        }

        public IServiceCollection Register(IServiceCollection services, IEnumerable<Type> rootQuery, IEnumerable<Type> rootMutation)
        {
            var baseSchema = Schema.Create(c =>
            {
                c.RegisterExtendedScalarTypes();
                c.Use(Middleware());
                c.RegisterQueryType(typeof(BaseQuery));
            });

            var domainSchemas = rootQuery.Zip(rootMutation, (a,b) => (a,b))
                .Select(t => Schema.Create(c =>            
            {
                c.RegisterExtendedScalarTypes();
                c.Use(Middleware(t.Item1, t.Item2));
                if (t.Item1 != null)
                    c.RegisterQueryType(typeof(ObjectType<>).MakeGenericType(t.Item1));

                if (t.Item2 != null)
                    c.RegisterMutationType(typeof(ObjectType<>).MakeGenericType(t.Item2));
            })).ToList();

            void AggregateSchemas(IStitchingBuilder b)
            {
                b.AddSchema("Base", baseSchema);
                var i = 0;
                foreach (var s in domainSchemas)
                {
                    b = b.AddSchema($"Domain{i}", s);//.IgnoreField($"Domain{i}","CreateRootInput","timestamp");
                    i++;
                }
            }
           
            if(services == null)
                services = new ServiceCollection();
            
            services.AddStitchedSchema(AggregateSchemas);

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