using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.GraphQL;
using ZES.Interfaces.Pipes;
using ZES.Interfaces.Replicas;
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
        private readonly IServiceCollection _services;
        private readonly IMessageQueue _messageQueue;
        private readonly IRecordLog _recordLog;
        private readonly IRemote _remote;
        private readonly ITimeline _timeline;

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaProvider"/> class.
        /// </summary>
        /// <param name="bus">Message bus</param>
        /// <param name="log">Application error log</param>
        /// <param name="manager">Branch manager service</param>
        /// <param name="mutations">GraphQL mutations</param>
        /// <param name="queries">GraphQL queries</param>
        /// <param name="services">Asp.Net services collection</param>
        /// <param name="graph">QGraph</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="recordLog">Record log</param>
        /// <param name="remote">Remote service</param>
        /// <param name="timeline">Timeline</param>
        public SchemaProvider(IBus bus, ILog log, IBranchManager manager, IEnumerable<IGraphQlMutation> mutations, IEnumerable<IGraphQlQuery> queries, IServiceCollection services, IGraph graph, IMessageQueue messageQueue, IRecordLog recordLog, IRemote remote, ITimeline timeline)
        {
            _bus = bus;
            _log = log;
            _manager = manager;
            _mutations = mutations;
            _queries = queries;
            _services = services;
            _graph = graph;
            _messageQueue = messageQueue;
            _recordLog = recordLog;
            _remote = remote;
            _timeline = timeline;

            InitialiseServices();
        }

        /// <inheritdoc />
        public IRequestExecutor Build()
        {
            var provider = _services.BuildServiceProvider();
            var resolver = provider.GetService<IRequestExecutorResolver>();
            var executor = resolver.GetRequestExecutorAsync().Result;
            
            /* var sender = provider.GetService<IEventSender>();

            _log.Logs.Subscribe(async m => await sender.SendAsync(
                new OnLogMessage(new LogMessage(m))));*/

            return executor;
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

            foreach (var pair in _log.StopWatch.Totals)
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
            _services.TryAddEnumerable(_queries.Select(ServiceDescriptor.Singleton));
            _services.TryAddEnumerable(_mutations.Select(ServiceDescriptor.Singleton));

            var builder = _services.AddGraphQL()
                .BindRuntimeType<Instant, InstantType>()
                .BindRuntimeType<Time, TimeType>()
                .AddType<CommandMetadataType>()
                .AddType<CommandStaticMetadataType>()
                .AddQueryType<BaseQueries>(c => c.Name("Query"))
                .AddMutationType<BaseMutations>(c => c.Name("Mutation"))
                .AddTypeExtension<QueryTypeExtensions>()
                .AddTypeExtension<MutationTypeExtensions>()
                .AddDiagnosticEventListener<DiagnosticListener>();

            foreach (var queryType in rootQuery)
            {
                foreach (var p in queryType.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                     BindingFlags.DeclaredOnly))
                {
                    var arg = p.GetParameters().FirstOrDefault()?.ParameterType;
                    if (arg != null && arg.GetInterfaces().Contains(typeof(IQuery)))
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
                    if (arg != null && arg.GetInterfaces().Contains(typeof(ICommand)))
                        builder.AddType(typeof(CommandType<>).MakeGenericType(arg));
                }

                // builder.AddMutationType(mutationType);
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
    }
}