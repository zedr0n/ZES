using System.Reflection;
using SimpleInjector;
using ZES.Infrastructure;
using ZES.Infrastructure.Attributes;
using ZES.Infrastructure.GraphQl;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
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
        
        public class Queries : GraphQlQuery 
        {
            public Queries(IBus bus)
                : base(bus)
            {
            }
            
            public RootInfo RootInfoQuery(string id) => Resolve(new RootInfoQuery(id));
            public RootInfo RootInfoQueryEx(RootInfoQuery query) => Resolve(query);
            public Stats StatsQuery() => Resolve(new StatsQuery());
            public Stats StatsQueryEx(StatsQuery query) => Resolve(query);
            public LastRecord LastRecordQuery(string id) => Resolve(new LastRecordQuery(id));
        }
        
        public class Mutations : GraphQlMutation
        {
            public Mutations(IBus bus, ILog log)
                : base(bus, log)
            {
            }

            public bool CreateRoot(string name) => Resolve(new CreateRoot(name));
            public bool CreateRootEx(CreateRoot command) => Resolve(command);
            public bool CreateRecord(string target) => Resolve(new CreateRecord(target));
            public bool CreateRecordEx(CreateRecord command) => Resolve(command);
            public bool RecordRoot(string target, double recordValue) => Resolve(new RecordRoot(target, recordValue));
            public bool RecordRootEx(RecordRoot command) => Resolve(command);
        }
    }
}