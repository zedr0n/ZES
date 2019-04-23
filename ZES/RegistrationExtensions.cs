using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleInjector;
using ZES.Infrastructure;
using ZES.Infrastructure.Projections;
using ZES.Infrastructure.Sagas;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Sagas;

namespace ZES
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
                
                var parameters = handler?.GetConstructors()[0].GetParameters();
                var tState = parameters?.First(p => p.ParameterType.GetInterfaces().Contains(typeof(IProjection)))
                    .ParameterType.GenericTypeArguments[0];

                var iHistoricalQuery = typeof(HistoricalQuery<,>).MakeGenericType(q, result);
                var iHistoricalQueryHandler = typeof(IQueryHandler<,>).MakeGenericType(iHistoricalQuery, result);
                
                var historicalHandler = typeof(HistoricalQueryHandler<,,>).MakeGenericType(q, result, tState);

                c.RegisterConditional(iQueryHandler, handler, Lifestyle.Transient, x => !x.Handled);
                c.RegisterConditional(iHistoricalQueryHandler, historicalHandler, Lifestyle.Transient, x => !x.Handled);
            }
        }
        
        /// <summary>
        /// Registers projections with <see cref="SimpleInjector"/> container
        /// </summary>
        /// <param name="c"><see cref="SimpleInjector"/> container</param>
        /// <param name="assembly">Assembly containing the domain to register</param>
        public static void RegisterProjections(this Container c, Assembly assembly)
        {
            var projections = assembly.GetTypesFromInterface(typeof(IProjection));
            foreach (var p in projections)
            {
                var projectionInterface = p.GetInterfaces().First(x => x.Name.StartsWith(nameof(IProjection)));
                var tState = projectionInterface.GenericTypeArguments[0];

                c.RegisterConditional(
                    projectionInterface,
                    typeof(HistoricalDecorator<>).MakeGenericType(tState),
                    Lifestyle.Transient,
                    x => x.Consumer.ImplementationType.IsClosedTypeOf(typeof(HistoricalQueryHandler<,,>)));
                c.RegisterConditional(projectionInterface, p, Lifestyle.Singleton, x => !x.Handled); 
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
        }
        
        private static IEnumerable<Type> GetTypesFromInterface(this Assembly assembly, Type t)
        {
            var types = assembly.GetTypes()
                .Where(p => p.GetInterfaces().Contains(t));
            return types;
        }
    }
}