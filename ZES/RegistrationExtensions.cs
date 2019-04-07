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
            }
        }
        
        public static void RegisterProjections(this Container c, Assembly assembly)
        {
            var projections = assembly.GetTypesFromInterface(typeof(IProjection));
            foreach (var p in projections)
                c.Register(p,p,Lifestyle.Singleton);

        }

        public static HistoricalProjection<TProjection,TState> GetHistorical<TProjection,TState>(this Container c) where TState : new()
            where TProjection : Projection<TState>
        {
            return c.GetInstance(typeof(HistoricalProjection<,>).MakeGenericType(typeof(TProjection), typeof(TState))) as HistoricalProjection<TProjection,TState>;
        }
        
        public static dynamic GetHistorical(this Container c, Type tProjection)
        {
            var tState = tProjection.GetInterfaces().First(i => i.GetGenericArguments().Length > 0).GetGenericArguments()[0];
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