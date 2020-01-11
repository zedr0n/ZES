using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleInjector;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Projections;
using ZES.Infrastructure.Sagas;
using ZES.Interfaces.Domain;
using ZES.Interfaces.GraphQL;
using ZES.Interfaces.Sagas;

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
        public static void RegisterAll(this Container c, Assembly assembly)
        {
            c.RegisterCommands(assembly);
            c.RegisterQueries(assembly);
            c.RegisterProjections(assembly);
            c.RegisterSagas(assembly);
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
                var iSaga = typeof(ISagaHandler<>).MakeGenericType(s);
                var handler = typeof(SagaHandler<>).MakeGenericType(s);
                c.Register(iSaga, handler, Lifestyle.Singleton);

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
                var result = q.GetInterfaces().SingleOrDefault(g => g.IsGenericType)?.GetGenericArguments().SingleOrDefault(); 
                if (result == null)
                    continue;
                
                var iQueryHandler = typeof(IQueryHandler<,>).MakeGenericType(q, result);
                var handler = assembly.GetTypesFromInterface(iQueryHandler).SingleOrDefault();

                if (handler == null)
                    handler = typeof(QueryHandlerBase<,>).MakeGenericType(q, result);
                
                var parameters = handler?.GetConstructors()[0].GetParameters();
                var tState = parameters?.First(p => p.ParameterType.GetInterfaces().Contains(typeof(IProjection)))
                    .ParameterType.GenericTypeArguments[0];

                var iHistoricalQuery = typeof(HistoricalQuery<,>).MakeGenericType(q, result);
                var iHistoricalQueryHandler = typeof(IQueryHandler<,>).MakeGenericType(iHistoricalQuery, result);
                
                var historicalHandler = typeof(HistoricalQueryHandler<,,>).MakeGenericType(q, result, tState);

                c.RegisterConditional(iQueryHandler, handler, Lifestyle.Singleton, x => !x.Handled);
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
            foreach (var p in projections.Where(p => !p.IsAbstract))
            {
                var projectionInterface = p.GetInterfaces().Where(i => i.IsGenericType).First(x => x.Name.StartsWith(nameof(IProjection)));
                var tState = projectionInterface.GenericTypeArguments[0];

                c.RegisterConditional(
                    projectionInterface,
                    typeof(HistoricalProjection<>).MakeGenericType(tState),
                    Lifestyle.Transient,
                    x => x.Consumer.ImplementationType.IsClosedTypeOf(typeof(HistoricalQueryHandler<,,>)));
                c.RegisterConditional(projectionInterface, p, Lifestyle.Singleton, x => !x.Handled);

                // var builderType = typeof(Projection<>.Dispatcher.Builder).MakeGenericType(tState);
                // var streamType = typeof(Projection<>.Slice.Builder).MakeGenericType(tState);
                // c.Register(builderType, builderType, Lifestyle.Singleton);
                // c.Register(streamType, streamType, Lifestyle.Singleton);
            }
        }

        /// <summary>
        /// Registers commands with <see cref="SimpleInjector"/> container
        /// </summary>
        /// <param name="c"><see cref="SimpleInjector"/> container</param>
        /// <param name="assembly">Assembly containing the domain to register</param>
        public static void RegisterCommands(this Container c, Assembly assembly)
        {
            var commands = assembly.GetTypesFromInterface(typeof(ICommand));
            foreach (var command in commands)
            {
                var iHandler = typeof(ICommandHandler<>).MakeGenericType(command);
                var handler = assembly.GetTypesFromInterface(iHandler).SingleOrDefault();
                c.Register(iHandler, handler, Lifestyle.Singleton);
            }

            var mutations = assembly.GetTypesFromInterface(typeof(IGraphQlMutation));
            foreach (var mutation in mutations)
               c.Collection.Append(typeof(IGraphQlMutation), mutation ); 
        }

        private static IEnumerable<Type> GetTypesFromInterface(this Assembly assembly, Type t)
        {
            var types = assembly.GetTypes()
                .Where(p => p.GetInterfaces().Contains(t));
            return types;
        }
    }
}