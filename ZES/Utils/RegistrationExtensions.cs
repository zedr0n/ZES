using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleInjector;
using ZES.Infrastructure;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Net;
using ZES.Infrastructure.Projections;
using ZES.Infrastructure.Serialization;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.GraphQL;
using ZES.Interfaces.Net;

namespace ZES.Utils
{
    /// <summary>
    /// Domain registration methods for <see cref="SimpleInjector"/> DI
    /// </summary>
    public static class RegistrationExtensions
    {
        /// <summary>
        /// Registers commands, queries, projections and sagas with <see cref="SimpleInjector"/> container
        /// </summary>
        /// <param name="c"><see cref="SimpleInjector"/> container</param>
        /// <param name="assembly">Assembly containing the domain to register</param>
        /// <param name="useSagas">Enable sagas</param>
        public static void RegisterAll(this Container c, Assembly assembly, bool useSagas = true)
        {
            c.RegisterEvents(assembly);
            c.RegisterAlerts(assembly);
            c.RegisterJsonHandlers(assembly);
            c.RegisterCommands(assembly);
            c.RegisterQueries(assembly);
            c.RegisterProjections(assembly);
            c.RegisterAggregates(assembly);
            if (useSagas)
                c.RegisterSagas(assembly);
        }

        /// <summary>
        /// Registers aggregates with <see cref="SimpleInjector"/> container
        /// </summary>
        /// <param name="c"><see cref="SimpleInjector"/> container</param>
        /// <param name="assembly">Assembly containing the domain to register</param>
        public static void RegisterAggregates(this Container c, Assembly assembly)
        {
            var aggregates = assembly.GetTypesFromInterface(typeof(IAggregate)).Where(t => !t.IsAbstract);
            foreach (var a in aggregates)
            {
                var m = typeof(EventSourced).GetMethod(nameof(EventSourced.Create), BindingFlags.Static | BindingFlags.Public)
                    ?.MakeGenericMethod(a);
                if (m != null)
                {
                    var reg = Lifestyle.Singleton.CreateRegistration(typeof(IEventSourced), () => m.Invoke(null, new object[] { string.Empty, 0 }), c);
                    c.Collection.Append(typeof(IEventSourced), reg);
                }

                var command = typeof(CreateSnapshot<>).MakeGenericType(a);
                var iHandler = typeof(ICommandHandler<>).MakeGenericType(command);
                var handler = typeof(CreateSnapshotHandler<>).MakeGenericType(a);
                c.RegisterConditional(iHandler, handler, Lifestyle.Singleton, x => !x.Handled);
            }
        }

        /// <summary>
        /// Register events with <see cref="SimpleInjector"/> container
        /// </summary>
        /// <param name="c"><see cref="SimpleInjector"/> container</param>
        /// <param name="assembly">Assembly containing the domain to register</param>
        public static void RegisterEvents(this Container c, Assembly assembly)
        {
            var deserializers = assembly.GetTypesFromInterface(typeof(IEventDeserializer)).Where(t => !t.IsAbstract);
            foreach (var d in deserializers)
                c.Collection.Append(typeof(IEventDeserializer), d);

            var serializers = assembly.GetTypesFromInterface(typeof(IEventSerializer)).Where(t => !t.IsAbstract);
            foreach (var s in serializers)
                c.Collection.Append(typeof(IEventSerializer), s);
        }

        /// <summary>
        /// Registers sagas with <see cref="SimpleInjector"/> container
        /// </summary>
        /// <param name="c"><see cref="SimpleInjector"/> container</param>
        /// <param name="assembly">Assembly containing the domain to register</param>
        public static void RegisterSagas(this Container c, Assembly assembly)
        {
            var sagas = assembly.GetTypesFromInterface(typeof(ISaga)).Where(t => !t.IsAbstract);
            foreach (var s in sagas)
            {
                var m = typeof(EventSourced).GetMethod(nameof(EventSourced.Create), BindingFlags.Static | BindingFlags.Public)
                    ?.MakeGenericMethod(s);
                if (m != null)
                {
                    var reg = Lifestyle.Singleton.CreateRegistration(typeof(IEventSourced), () => m.Invoke(null, new object[] { string.Empty, 0 }), c);
                    c.Collection.Append(typeof(IEventSourced), reg);
                }
                
                var iHandler = typeof(ISagaHandler<>).MakeGenericType(s);
                var handler = typeof(SagaHandler<>).MakeGenericType(s);
                c.Register(iHandler, handler, Lifestyle.Singleton);

                var dispatcherType = typeof(SagaHandler<>.SagaDispatcher.Builder).MakeGenericType(s);
                var flowType = typeof(SagaHandler<>.SagaDispatcher.SagaFlow.Builder).MakeGenericType(s);
                c.Register(dispatcherType, dispatcherType, Lifestyle.Singleton);
                c.Register(flowType, flowType, Lifestyle.Singleton);
            }
        }

        /// <summary>
        /// Registers queries with <see cref="SimpleInjector"/> container
        /// </summary>
        /// <param name="c"><see cref="SimpleInjector"/> container</param>
        /// <param name="assembly">Assembly containing the domain to register</param>
        public static void RegisterQueries(this Container c, Assembly assembly)
        {
            var queries = assembly.GetTypes().Where(t => t.IsClosedTypeOf(typeof(IQuery<>)));
            foreach (var q in queries)
            {
                var result = q.GetInterfaces().FirstOrDefault(g => g.IsGenericType)?.GetGenericArguments().SingleOrDefault(); 
                if (result == null)
                    continue;
                
                var iQueryHandler = typeof(IQueryHandler<,>).MakeGenericType(q, result);
                var handler = assembly.GetTypesFromInterface(iQueryHandler).SingleOrDefault();
                var isSingle = q.GetTypeInfo().IsClosedTypeOf(typeof(ISingleQuery<>)); 

                if (handler == null)
                {
                    handler = isSingle ?
                        typeof(DefaultSingleQueryHandler<,>).MakeGenericType(q, result) :
                        typeof(DefaultQueryHandler<,>).MakeGenericType(q, result);
                }

                var baseType = handler.BaseType;
                var tState = result;
                while (baseType != null && baseType.IsGenericType)
                {
                    var parameters = baseType.GetGenericArguments();
                    if (parameters.Length == 3)
                    {
                        tState = parameters[2];
                        break;
                    }

                    baseType = baseType.BaseType;
                }

                var iHistoricalQuery = typeof(HistoricalQuery<,>).MakeGenericType(q, result);
                var iHistoricalQueryHandler = typeof(IQueryHandler<,>).MakeGenericType(iHistoricalQuery, result);
                
                var historicalHandler = typeof(HistoricalQueryHandler<,,>).MakeGenericType(q, result, tState);
                if (isSingle)
                    historicalHandler = typeof(HistoricalSingleQueryHandler<,,>).MakeGenericType(q, result, tState);

                var lifestyle = isSingle ? Lifestyle.Transient : Lifestyle.Singleton; 
                if (handler.GetCustomAttribute<TransientAttribute>() != null)
                    lifestyle = Lifestyle.Transient;
                
                c.RegisterConditional(iQueryHandler, handler, lifestyle, x => !x.Handled);
                c.RegisterConditional(iHistoricalQueryHandler, historicalHandler, Lifestyle.Transient, x => !x.Handled);
            }

            foreach (var q in assembly.GetTypesFromInterface(typeof(IGraphQlQuery)))
                c.Collection.Append(typeof(IGraphQlQuery), q);
        }
        
        /// <summary>
        /// Registers projections with <see cref="SimpleInjector"/> container
        /// </summary>
        /// <param name="c"><see cref="SimpleInjector"/> container</param>
        /// <param name="assembly">Assembly containing the domain to register</param>
        public static void RegisterProjections(this Container c, Assembly assembly)
        {
            var projections = assembly.GetTypesFromInterface(typeof(IProjection))
                .Where(p => p.IsClosedTypeOf(typeof(IProjection<>)));

            var registeredStates = new List<Type>();
            foreach (var p in projections.Where(p => !p.IsAbstract))
            {
                var projectionInterface = p.GetInterfaces().Where(i => i.IsGenericType).First(x => x.Name.StartsWith(nameof(IProjection)));
                var tState = projectionInterface.GenericTypeArguments[0];
                registeredStates.Add(tState);

                c.RegisterConditional(
                    projectionInterface,
                    typeof(HistoricalProjection<>).MakeGenericType(tState),
                    Lifestyle.Transient,
                    x => x.Consumer != null && x.Consumer.ImplementationType.IsClosedTypeOf(typeof(HistoricalQueryHandler<,,>)));
                
                c.RegisterConditional(
                    typeof(IHistoricalProjection<>).MakeGenericType(tState),
                    typeof(HistoricalProjection<>).MakeGenericType(tState),
                    Lifestyle.Transient,
                    x => !x.Handled);
                
                // var lifestyle = tState.GetInterfaces().Contains(typeof(ISingleState)) ? Lifestyle.Transient : Lifestyle.Singleton;
                var lifestyle = Lifestyle.Transient;
                c.RegisterConditional(projectionInterface, p, lifestyle, x => !x.Handled);
            }

            var otherStates = assembly.GetTypesFromInterface(typeof(IState)).Where(t => !registeredStates.Contains(t));
            foreach (var tState in otherStates)
            {
                var projectionInterface = typeof(IProjection<>).MakeGenericType(tState);
                var projection = typeof(DefaultProjection<>).MakeGenericType(tState);
                
                c.RegisterConditional(
                    projectionInterface,
                    typeof(HistoricalProjection<>).MakeGenericType(tState),
                    Lifestyle.Transient,
                    x => x.Consumer != null && x.Consumer.ImplementationType.IsClosedTypeOf(typeof(HistoricalQueryHandler<,,>)));
                c.RegisterConditional(
                    typeof(IHistoricalProjection<>).MakeGenericType(tState),
                    typeof(HistoricalProjection<>).MakeGenericType(tState),
                    Lifestyle.Transient,
                    x => !x.Handled);
                
                // var lifestyle = tState.GetInterfaces().Contains(typeof(ISingleState)) ? Lifestyle.Transient : Lifestyle.Singleton;
                var lifestyle = Lifestyle.Transient;
                c.RegisterConditional(projectionInterface, projection, lifestyle, x => !x.Handled);

                var tHandler = typeof(IProjectionHandler<>).MakeGenericType(tState);
                var handlers = assembly.GetTypesFromInterface(tHandler);
                foreach (var t in handlers)
                {
                    var reg = Lifestyle.Singleton.CreateRegistration(t, c);
                    c.Collection.Append(tHandler, reg);    
                }
            }
        }

        /// <summary>
        /// Register the alert handlers
        /// </summary>
        /// <param name="c"><see cref="SimpleInjector"/> container</param>
        /// <param name="assembly">Assembly containing the domain to register</param>
        public static void RegisterAlerts(this Container c, Assembly assembly)
        {
            var handlers = assembly.GetTypesFromInterface(typeof(IAlertHandler))
                .Where(h => !h.IsAbstract);
            foreach (var h in handlers)
            {
                var iHandler = h.GetAsClosedInterfaceOf(typeof(IAlertHandler<>)); 
                c.RegisterConditional(iHandler, h, Lifestyle.Singleton, x => !x.Handled);
            } 
        }

        /// <summary>
        /// Register the handlers for json responses
        /// </summary>
        /// <param name="c"><see cref="SimpleInjector"/> container</param>
        /// <param name="assembly">Assembly containing the domain to register</param>
        public static void RegisterJsonHandlers(this Container c, Assembly assembly)
        {
            var results = assembly.GetTypesFromInterface(typeof(IJsonResult));

            foreach (var r in results)
            {
                var iHandler = typeof(IJsonHandler<>).MakeGenericType(r);
                var handler = assembly.GetTypesFromInterface(iHandler).SingleOrDefault();
                if (handler == null)
                    handler = typeof(DefaultJsonHandler<>).MakeGenericType(r);

                c.Register(iHandler, handler, Lifestyle.Singleton);
            } 
        }

        /// <summary>
        /// Registers commands with <see cref="SimpleInjector"/> container
        /// </summary>
        /// <param name="c"><see cref="SimpleInjector"/> container</param>
        /// <param name="assembly">Assembly containing the domain to register</param>
        public static void RegisterCommands(this Container c, Assembly assembly)
        {
            var commands = assembly.GetTypesFromInterface(typeof(ICommand)).ToList();
            foreach (var command in commands)
            {
                var iHandler = typeof(ICommandHandler<>).MakeGenericType(command);
                var handler = assembly.GetTypesFromInterface(iHandler).FirstOrDefault();
                if (handler == null)
                    return;

                var iRetroactiveCommand = typeof(RetroactiveCommand<>).MakeGenericType(command);
                var iRetroactiveCommandHandler = typeof(ICommandHandler<>).MakeGenericType(iRetroactiveCommand);
                
                var retroactiveCommandHandler =
                    typeof(RetroactiveCommandHandler<>).MakeGenericType(command);

                if (command.ContainsGenericParameters)
                {
                    var type = command.GetGenericArguments().SingleOrDefault()?.GetInterfaces().SingleOrDefault();
                    var allTypes = assembly.GetTypesFromInterface(type);
                    foreach (var t in allTypes)
                    {
                        var closedCommand = command.MakeGenericType(t);
                        var iClosedHandler = typeof(ICommandHandler<>).MakeGenericType(closedCommand);
                        var closedHandler = handler.MakeGenericType(t);
                        var baseType = closedCommand.BaseType;
                            
                        c.RegisterConditional(iClosedHandler, closedHandler, Lifestyle.Singleton, x => !x.Handled);
                        if (baseType != null && !baseType.ContainsGenericParameters)
                            c.Collection.Append(typeof(ICommandHandler<>).MakeGenericType(baseType), closedHandler);

                        var closedRetroactiveCommand = typeof(RetroactiveCommand<>).MakeGenericType(closedCommand); 
                        var iClosedRetroactiveCommandHandler = typeof(ICommandHandler<>).MakeGenericType(closedRetroactiveCommand);
                        var closedRetroactiveCommandHandler = typeof(RetroactiveCommandHandler<>).MakeGenericType(closedCommand); 
                        c.RegisterConditional(iClosedRetroactiveCommandHandler, closedRetroactiveCommandHandler, Lifestyle.Singleton, x => !x.Handled);
                    }
                }
                else
                {
                    c.RegisterConditional(iHandler, handler, Lifestyle.Singleton, x => !x.Handled);
                    c.RegisterConditional(iRetroactiveCommandHandler, retroactiveCommandHandler, Lifestyle.Singleton, x => !x.Handled);
                }
            }

            var jsonResults = assembly.GetTypesFromInterface(typeof(IJsonResult));
            foreach (var t in jsonResults)
            {
                var iHandler = typeof(ICommandHandler<>).MakeGenericType(typeof(RequestJson<>).MakeGenericType(t));
                var handler = typeof(JsonRequestHandler<>).MakeGenericType(t);
                c.RegisterConditional(iHandler, handler, Lifestyle.Singleton, x => !x.Handled);
            }

            var mutations = assembly.GetTypesFromInterface(typeof(IGraphQlMutation));
            foreach (var mutation in mutations)
               c.Collection.Append(typeof(IGraphQlMutation), mutation ); 
        }

        private static IEnumerable<Type> GetTypesFromInterface(this Assembly assembly, Type t)
        {
            var types = assembly.GetTypes()
                .Where(p => p.GetInterfaces().Any(x => x.GUID == t.GUID && x.FullName == t.FullName));
            return types;
        }

        private static Type GetAsClosedTypeOf(this Type t, Type genericTypeDefinition)
        {
            while (t != null && (!t.IsGenericType || t.GetGenericTypeDefinition() != genericTypeDefinition))
                t = t.BaseType;
            return t;
        }
        
        private static Type GetAsClosedInterfaceOf(this Type t, Type genericTypeDefinition)
        {
            var interfaces = t.GetInterfaces().ToList();
            return interfaces.SingleOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericTypeDefinition);
        }
    }
}