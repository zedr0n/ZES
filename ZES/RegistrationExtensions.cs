using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimpleInjector;
using ZES.Infrastructure.Projections;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Sagas;

namespace ZES
{
    public static class RegistrationExtensions
    {
        public static void RegisterAll(this Container c, Assembly assembly)
        {
            c.RegisterCommands(assembly);
            c.RegisterQueries(assembly);
            c.RegisterProjections(assembly);
            c.RegisterSagas(assembly);
        }
        public static IEnumerable<Type> GetTypesFromInterface(this Assembly assembly, Type t)
        {
            var types = assembly.GetTypes()
                .Where(p => p.GetInterfaces().Contains(t));
            return types;
        }

        public static void RegisterSagas(this Container c, Assembly assembly)
        {
            var sagas = assembly.GetTypesFromInterface(typeof(ISaga)).Where(t => !t.IsAbstract);
            foreach (var s in sagas)
            {
                var iSaga = typeof(ISagaHandler<>).MakeGenericType(s);
                var handler = assembly.GetTypesFromInterface(iSaga).SingleOrDefault();
                if(handler != null)
                    c.Register(iSaga, handler);
            }
        }

        public static void RegisterQueries(this Container c, Assembly assembly)
        {
            var queries = assembly.GetTypesFromInterface(typeof(IQuery));
            foreach (var q in queries)
            {
                var result = q.GetInterfaces().SingleOrDefault(g => g.IsGenericType)?.GetGenericArguments().SingleOrDefault(); 
                if(result == null)
                    continue;
                
                var iQuery = typeof(IQueryHandler<,>).MakeGenericType(q, result);
                var handler = assembly.GetTypesFromInterface(iQuery).SingleOrDefault();
                c.Register(iQuery,handler, Lifestyle.Transient);
                               
                var iHistoricalHandler = typeof(IHistoricalQueryHandler<,>).MakeGenericType(q, result); 
                
                var parameters = handler.GetConstructors()[0].GetParameters();
                var tState = parameters.First(p => p.ParameterType.GetInterfaces().Contains(typeof(IProjection)))
                    .ParameterType.GenericTypeArguments[0];
                
                var historicalHandler = typeof(HistoricalQueryHandler<,,>).MakeGenericType(q, result,tState);
                c.Register(iHistoricalHandler, historicalHandler, Lifestyle.Transient);
                //c.RegisterConditional(iQuery,historicalHandler, Lifestyle.Transient,x => q.GetInterfaces().Contains(typeof(IHistoricalQuery)));
                //c.RegisterConditional(iQuery, handler, Lifestyle.Transient, x => !q.GetInterfaces().Contains(typeof(IHistoricalQuery)));
            }
        }
        
        public static void RegisterProjections(this Container c, Assembly assembly)
        {
            var projections = assembly.GetTypesFromInterface(typeof(IProjection));
            foreach (var p in projections)
            {
                var projectionInterface = p.GetInterfaces().First(x => x.Name.StartsWith(nameof(IProjection)));
                var tState = projectionInterface.GenericTypeArguments[0];

                //c.Register(projectionInterface, p, Lifestyle.Singleton);
                c.RegisterConditional(projectionInterface, typeof(HistoricalDecorator<>).MakeGenericType(tState),
                    Lifestyle.Transient,x => x.Consumer.ImplementationType.GetInterfaces().Contains(typeof(IHistoricalQueryHandler)));
                c.RegisterConditional(projectionInterface,p,Lifestyle.Singleton, x => !x.Handled); 
            }


        }

        public static HistoricalProjection<TProjection,TState> GetHistorical<TProjection,TState>(this Container c) where TState : new()
            where TProjection : Projection<TState>
        {
            return c.GetInstance(typeof(HistoricalProjection<,>).MakeGenericType(typeof(TProjection), typeof(TState))) as HistoricalProjection<TProjection,TState>;
        }
        
        public static dynamic GetHistorical(this Container c, Type tProjection)
        {
            Type tState;
            if (!tProjection.IsInterface)
                tState = tProjection.GetInterfaces().First(i => i.GetGenericArguments().Length > 0)
                    .GetGenericArguments()[0];
            else
                tState = tProjection.GenericTypeArguments[0];
            return c.GetInstance(typeof(HistoricalProjection<,>).MakeGenericType(tProjection, tState));
        }
        
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
    }

}