using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Resolvers;
using HotChocolate.Stitching;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;
using ZES.Infrastructure;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class SchemaProvider : ISchemaProvider
    {
        private readonly IBus _bus;
        private readonly IErrorLog _errorLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaProvider"/> class.
        /// </summary>
        /// <param name="bus">Message bus</param>
        /// <param name="errorLog">Application error log</param>
        public SchemaProvider(IBus bus, IErrorLog errorLog)
        {
            _bus = bus;
            _errorLog = errorLog;
        }

        /// <inheritdoc />
        public IServiceCollection Register(IServiceCollection services, IEnumerable<Type> rootQuery, IEnumerable<Type> rootMutation)
        {
            var baseSchema = Schema.Create(c =>
            {
                c.RegisterExtendedScalarTypes();
                c.Use(Middleware());
                c.RegisterQueryType(typeof(BaseQuery));
            });

            var domainSchemas = rootQuery.Zip(rootMutation, (a, b) =>
                                                                (a, b))
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
                    b = b.AddSchema($"Domain{i}", s);
                    i++;
                }
            }
           
            if (services == null)
                services = new ServiceCollection();
            
            services.AddStitchedSchema(AggregateSchemas);

            return services;
        }

        /// <inheritdoc />
        public IQueryExecutor Generate(Type rootQuery = null, Type rootMutation = null)
        {
            var services = Register(null, rootQuery, rootMutation);
                    
            var executor = services.BuildServiceProvider().GetService<IQueryExecutor>();
            return executor;
        }
        
        private IServiceCollection Register(IServiceCollection services, Type rootQuery, Type rootMutation)
        {
            return Register(services, new[] { rootQuery }, new[] { rootMutation });
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
    }
}