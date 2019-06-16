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
using SimpleInjector;
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
        private readonly IErrorLog _errorLog;
        private readonly IGraphQlGenerator _generator;

        private readonly List<Type> _commands = new List<Type>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaProvider"/> class.
        /// </summary>
        /// <param name="bus">Message bus</param>
        /// <param name="errorLog">Application error log</param>
        /// <param name="container">SimpleInjector container</param>
        /// <param name="generator">GraphQL generator</param>
        public SchemaProvider(IBus bus, IErrorLog errorLog, Container container, IGraphQlGenerator generator)
        {
            _bus = bus;
            _errorLog = errorLog;
            _generator = generator;

            var handlers = container.GetCurrentRegistrations()
                .Select(p => p.Registration.ImplementationType)
                .Where(t => t.IsClosedTypeOf(typeof(ICommandHandler<>)));
            var commands =
                handlers.Select(t => t.GetInterfaces().Select(i => i.GetGenericArguments().FirstOrDefault()).FirstOrDefault());
            _commands.AddRange(commands);
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
                    var isError = false;
                    _errorLog.Errors.Subscribe(e =>
                    {
                        if (e != null && e.ErrorType == typeof(InvalidOperationException).Name)
                            isError = true;
                    });

                    var input = field.GetParameters().FirstOrDefault();
                    dynamic command = null;
                    if (_commands.Any(c => c.Name == input?.ParameterType.Name))
                    {
                        command = context.Argument<object>(input.Name);
                    }
                    else
                    {
                        var parameters = field.GetParameters().Select(p => context.Argument<object>(p.Name)).ToArray();
                        var commandType = _commands.SingleOrDefault(c => c.Name == field.Name);
                        if (commandType != null)
                            command = Activator.CreateInstance(commandType, parameters);
                    }

                    if (command != null)
                        await await _bus.CommandAsync(command);
                    else
                        isError = true;

                    context.Result = !isError;
                    return;
                }

                await next.Invoke(context);
            };
        }
    }
}