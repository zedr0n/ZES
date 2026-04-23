#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Configuration;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using NodaTime;
using ZES.Infrastructure.GraphQl;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.GraphQL;
using ZES.Interfaces.Infrastructure;
using ZES.Interfaces.Recording;
using ZES.Utils;
using IError = HotChocolate.IError;
using IQuery = ZES.Interfaces.Domain.IQuery;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class SchemaProvider : ISchemaProvider
    {
        private readonly IBus _bus;
        private readonly ILog _log;
        private readonly IGraph _graph;
        private readonly IBranchManager _manager;
        private readonly IEnumerable<IGraphQlMutation> _mutations;
        private readonly IEnumerable<IGraphQlQuery> _queries;
        private readonly IEnumerable<ICatalog<IGraphQlInputType>> _inputTypes;
        private readonly IServiceCollection _services;
        private readonly IMessageQueue _messageQueue;
        private readonly IRecordLog _recordLog;
        private readonly IRemote _remote;
        private readonly ITimeline _timeline;
        private readonly GraphQlResolver _graphQlResolver;

        /// <summary>
        /// Provides the implementation for the GraphQL schema provider.
        /// </summary>
        /// <param name="bus">Message bus</param>
        /// <param name="log">Application error log</param>
        /// <param name="manager">Branch manager service</param>
        /// <param name="mutations">Collection of GraphQL mutations</param>
        /// <param name="queries">Collection of GraphQL queries</param>
        /// <param name="inputTypes">Collection of GraphQL input types</param>
        /// <param name="services">ASP.NET Core service collection</param>
        /// <param name="graph">Graph service</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="recordLog">Record log service</param>
        /// <param name="remote">Remote service</param>
        /// <param name="timeline">Timeline service</param>
        /// <param name="graphQlResolver">GraphQL Resolver</param>
        public SchemaProvider(IBus bus, ILog log, IBranchManager manager, IEnumerable<IGraphQlMutation> mutations,
            IEnumerable<IGraphQlQuery> queries, IEnumerable<ICatalog<IGraphQlInputType>> inputTypes,
            IServiceCollection services, IGraph graph, IMessageQueue messageQueue, IRecordLog recordLog, IRemote remote,
            ITimeline timeline, GraphQlResolver graphQlResolver)
        {
            _bus = bus;
            _log = log;
            _manager = manager;
            _mutations = mutations;
            _queries = queries;
            _inputTypes = inputTypes;
            _services = services;
            _graph = graph;
            _messageQueue = messageQueue;
            _recordLog = recordLog;
            _remote = remote;
            _timeline = timeline;
            _graphQlResolver = graphQlResolver;

            InitialiseServices();
        }

        /// <inheritdoc />
        public IRequestExecutor Build()
        {
            var provider = _services.BuildServiceProvider();
            var resolver = provider.GetService<IRequestExecutorResolver>();
            var executor = resolver?.GetRequestExecutorAsync().Result;
            
            /* var sender = provider.GetService<IEventSender>();

            _log.Logs.Subscribe(async m => await sender.SendAsync(
                new OnLogMessage(new LogMessage(m))));*/

            return executor!;
        }

        /// <inheritdoc />
        public async Task<ReplayResult> Replay(IScenario scenario)
        {
            var executor = Build();

            var sw = Stopwatch.StartNew();
            foreach (var m in scenario.Requests)
                await executor.ExecuteAsync(m.GraphQl);

            foreach (var r in scenario.Results)
                await executor.ExecuteAsync(r.GraphQl);

            foreach (var pair in _log.StopWatch.Totals.ToImmutableSortedDictionary())
                _log.Info($"{pair.Key} : {pair.Value}ms");
            return new ReplayResult(sw.ElapsedMilliseconds);
        }

        private void InitialiseServices()
        {
            var rootQuery = _queries.Select(t => t.GetType());
            var rootMutation = _mutations.Select(t => t.GetType());
            
            _services.AddSingleton(typeof(ILog), _log);
            _services.AddSingleton(typeof(IBus), _bus);
            _services.AddSingleton(typeof(IBranchManager), _manager);
            _services.AddSingleton(typeof(IGraph), _graph);
            _services.AddSingleton(typeof(IMessageQueue), _messageQueue);
            _services.AddSingleton(typeof(IRecordLog), _recordLog);
            _services.AddSingleton(typeof(IRemote), _remote);
            _services.AddSingleton(typeof(ITimeline), _timeline);
            _services.AddSingleton(typeof(GraphQlResolver), _graphQlResolver);
            _services.TryAddEnumerable(_queries.Select(ServiceDescriptor.Singleton));
            _services.TryAddEnumerable(_mutations.Select(ServiceDescriptor.Singleton));

            var builder = _services.AddGraphQL()
                .BindRuntimeType<Instant, InstantType>()
                .BindRuntimeType<Time, TimeType>()
                .AddType<CommandMetadataType>()
                .AddType<CommandStaticMetadataType>()
                .AddType<ErrorType>()
                .AddQueryType<BaseQueries>(c => c.Name("Query"))
                .AddMutationType<BaseMutations>(c => c.Name("Mutation"))
                .AddTypeExtension<QueryTypeExtensions>()
                .AddTypeExtension<MutationTypeExtensions>()
                .AddDiagnosticEventListener<DiagnosticListener>()
                .TryAddTypeInterceptor<JsonIgnoreTypeInterceptor>();

            foreach (var type in _inputTypes.Aggregate(new HashSet<Type>(), (types, catalog) => types.Union(catalog.Types).ToHashSet()))
            {
                // Get the primary constructor (the one with most parameters, or you can use other logic)
                var constructor = type.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault();

                if (constructor == null) 
                    continue;
                
                // Get constructor parameter names
                var constructorParamNames = constructor.GetParameters()
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
        
                // Get all properties
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                var propsToIgnore = properties
                    .Where(p => !constructorParamNames.Contains(p.Name))
                    .Select(p => p.Name.FirstCharacterToLower())
                    .ToHashSet();
                
                var dynamicInputType = (INamedType?)Activator.CreateInstance(
                    typeof(IgnoreInputObjectType<>).MakeGenericType(type),
                    propsToIgnore,
                    $"{type.Name}Input");

                if (dynamicInputType != null) 
                    builder.AddType(dynamicInputType);
            }

            foreach (var queryType in rootQuery)
            {
                foreach (var p in queryType.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                     BindingFlags.DeclaredOnly))
                {
                    var arg = p.GetParameters().FirstOrDefault()?.ParameterType;
                    if (arg != null && Enumerable.Contains(arg.GetInterfaces(), typeof(IQuery)))
                        builder.AddType(typeof(QueryType<>).MakeGenericType(arg));
                }

                // builder.AddQueryType(queryType);
            }

            foreach (var mutationType in rootMutation)
            {
                foreach (var p in mutationType.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                     BindingFlags.DeclaredOnly))
                {
                    var arg = p.GetParameters().FirstOrDefault()?.ParameterType;
                    var isList = arg?.IsGenericType == true &&
                                 arg.GetGenericTypeDefinition() == typeof(List<>) &&
                                 Enumerable.Contains(arg.GetGenericArguments()[0].GetInterfaces(), typeof(ICommand));

                    var commandArg = isList ? arg?.GetGenericArguments()[0] : arg;

                    if (commandArg != null && Enumerable.Contains(commandArg.GetInterfaces(), typeof(ICommand)))
                    {
                        var commandType = typeof(CommandType<>).MakeGenericType(commandArg);
                        builder.AddType(commandType);
                    }
                }
            }
        }

        /// <inheritdoc />
        [UsedImplicitly]
        private class JsonIgnoreTypeInterceptor : TypeInterceptor
        {
            /// <inheritdoc />
            public override void OnBeforeRegisterDependencies(
                ITypeDiscoveryContext discoveryContext, DefinitionBase? definition,
                IDictionary<string, object?> contextData)
            {
                if (definition is not ObjectTypeDefinition objectDef) return;
                for (var i = objectDef.Fields.Count - 1; i >= 0; i--)
                {
                    if (objectDef.Fields[i].Member?.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                        objectDef.Fields.RemoveAt(i);
                }
            }
        }        
        
        [UsedImplicitly]
        private class QueryTypeExtensions : ObjectTypeExtension
        {
            private readonly IEnumerable<IGraphQlQuery> _queries;

            public QueryTypeExtensions(IEnumerable<IGraphQlQuery> queries)
            {
                _queries = queries;
            }
            
            protected override void Configure(IObjectTypeDescriptor descriptor)
            {
                descriptor.Name("Query");

                var rootQuery = _queries.Select(t => t.GetType());

                foreach (var queryType in rootQuery)
                {
                    foreach (var mi in queryType.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                            BindingFlags.DeclaredOnly))
                    {
                        descriptor.Field(mi.Name.FirstCharacterToLower())
                            .Type(mi.ReturnType)
                            .ResolveWith(mi);
                        
                        foreach (var arg in mi.GetParameters())
                        {
                            descriptor.Field(mi.Name.FirstCharacterToLower())
                                .Argument(arg.Name.FirstCharacterToLower(), argDescriptor => 
                                    argDescriptor.Type(arg.ParameterType));
                        }
                    }

                    foreach (var pi in queryType.GetProperties(BindingFlags.Public | BindingFlags.Instance |
                                                               BindingFlags.DeclaredOnly))
                    {
                        descriptor.Field(pi.Name.FirstCharacterToLower())
                            .Type(pi.PropertyType)
                            .ResolveWith(pi);
                    }
                }
                
                base.Configure(descriptor);
            }
        }

        [UsedImplicitly]
        private class MutationTypeExtensions : ObjectTypeExtension
        {
            private readonly IEnumerable<IGraphQlMutation> _mutations;

            public MutationTypeExtensions(IEnumerable<IGraphQlMutation> mutations)
            {
                _mutations = mutations;
            }
            
            protected override void Configure(IObjectTypeDescriptor descriptor)
            {
                descriptor.Name("Mutation");

                var rootMutation = _mutations.Select(t => t.GetType());

                foreach (var queryType in rootMutation)
                {
                    foreach (var mi in queryType.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                            BindingFlags.DeclaredOnly))
                    {
                        descriptor.Field(mi.Name.FirstCharacterToLower())
                            .Type(mi.ReturnType)
                            .ResolveWith(mi);

                        foreach (var arg in mi.GetParameters())
                        {
                            descriptor.Field(mi.Name.FirstCharacterToLower())
                                .Argument(arg.Name.FirstCharacterToLower(), argDescriptor => 
                                    argDescriptor.Type(arg.ParameterType));
                        }
                    }
                }
                
                base.Configure(descriptor);
            }
        }

        [UsedImplicitly]
        private class ErrorFilter : IErrorFilter
        {
            private readonly ILog _log;

            public ErrorFilter(ILog log)
            {
                _log = log;
            }

            public IError OnError(IError error)
            {
                var errorBuilder = new ErrorBuilder();
                var lastError = _log.Errors.Observable.FirstOrDefaultAsync()?.GetAwaiter().GetResult();
                errorBuilder.SetMessage(lastError?.Message ?? error.Exception?.Message ?? error.Message);
                error = errorBuilder.Build();

                return error;
            }
        }
        
        private class IgnoreInputObjectType<T>(HashSet<string> fieldsToIgnore, string typeName) : InputObjectType<T>
        {
            protected override void Configure(IInputObjectTypeDescriptor<T> descriptor)
            {
                descriptor.Name(typeName);
                base.Configure(descriptor);
            }

            protected override FieldCollection<InputField> OnCompleteFields(
                ITypeCompletionContext context,
                InputObjectTypeDefinition definition)
            {
                foreach (var field in definition.Fields.Where(f => fieldsToIgnore.Contains(f.Name)))
                    field.Ignore = true;

                return base.OnCompleteFields(context, definition);
            }
        }        
    }
}