using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Stitching;
using HotChocolate.Subscriptions;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;
using ZES.Interfaces;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Domain;
using ZES.Interfaces.GraphQL;
using ZES.Interfaces.Pipes;

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
        private readonly IDiagnosticObserver _observer;
        private readonly IRecordLog _recordLog;

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
        /// <param name="observer">Diagnostic observer</param>
        /// <param name="recordLog">Record log</param>
        public SchemaProvider(IBus bus, ILog log, IBranchManager manager, IEnumerable<IGraphQlMutation> mutations, IEnumerable<IGraphQlQuery> queries, IServiceCollection services, IGraph graph, IMessageQueue messageQueue, IDiagnosticObserver observer, IRecordLog recordLog)
        {
            _bus = bus;
            _log = log;
            _manager = manager;
            _mutations = mutations;
            _queries = queries;
            _services = services;
            _graph = graph;
            _messageQueue = messageQueue;
            _observer = observer;
            _recordLog = recordLog;

            InitialiseServices();
        }

        /// <inheritdoc />
        public IQueryExecutor Build()
        {
            var provider = _services.BuildServiceProvider();
            
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

        /// <inheritdoc />
        public async Task<ReplayResult> Replay(IScenario scenario)
        {
            var executor = Build();

            var sw = Stopwatch.StartNew();
            foreach (var m in scenario.Requests)
                await executor.ExecuteAsync(m.GraphQl);

            foreach (var r in scenario.Results)
                await executor.ExecuteAsync(r.GraphQl);

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
            _services.AddDiagnosticObserver(_observer);

            _services.AddInMemorySubscriptionProvider();
            
            var baseSchema = Schema.Create(c =>
            {
                c.RegisterExtendedScalarTypes();
                
                // c.Use(Middleware());
                c.RegisterQueryType(typeof(BaseQueries));
                c.RegisterMutationType(typeof(BaseMutations));
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
           
            _services.AddStitchedSchema(AggregateSchemas);
        }
    }
}