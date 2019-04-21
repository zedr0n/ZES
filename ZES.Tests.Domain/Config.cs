using System.Reflection;
using SimpleInjector;
using ZES.Infrastructure.Attributes;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;

namespace ZES.Tests.Domain
{
    public static class Config
    {
        [RootQuery]
        public class Queries
        {
            public CreatedAt CreatedAt(CreatedAtQuery query) => null;
            public Stats Stats(StatsQuery query) => null;
        }
        
        [RootMutation]
        public class Mutations
        {
            public bool CreateRoot(CreateRoot command) => true;
        }
        
        [Registration]
        public static void RegisterAll(Container c)
        {
            c.RegisterAll(Assembly.GetExecutingAssembly());
        }
        
        public static void RegisterProjections(Container c)
        {
            c.RegisterProjections(Assembly.GetExecutingAssembly());
        }
        
        public static void RegisterSagas(Container c)
        {
            c.RegisterSagas(Assembly.GetExecutingAssembly());
        }

        public static void RegisterCommands(Container c)
        {
            c.RegisterCommands(Assembly.GetExecutingAssembly());
        }

        public static void RegisterQueries(Container c)
        {
            c.RegisterQueries(Assembly.GetExecutingAssembly());
        }
    }
}