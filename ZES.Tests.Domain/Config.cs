using System.Reflection;
using SimpleInjector;
using ZES.Infrastructure.Attributes;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;
using ZES.Utils;

namespace ZES.Tests.Domain
{
    public static class Config
    {
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

        [RootQuery]
        public abstract class Queries
        {
            public abstract RootInfo RootInfoQuery(string id);
            public abstract RootInfo RootInfoQueryEx(RootInfoQuery query);
            public abstract Stats StatsQuery();
            public abstract Stats StatsQueryEx(StatsQuery query);
            public abstract LastRecord LastRecordQuery(string id);
        }
        
        [RootMutation]
        public abstract class Mutations
        {
             public abstract bool CreateRoot(string name);
             public abstract bool CreateRootEx(CreateRoot command);
             public abstract bool CreateRecord(string target);
             public abstract bool CreateRecordEx(CreateRecord command);
             public abstract bool RecordRoot(string target, double recordValue);
             public abstract bool RecordRootEx(RecordRoot command);
        }
    }
}