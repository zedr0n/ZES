using System.Reflection;
using SimpleInjector;
using ZES.Infrastructure;
using ZES.Infrastructure.Attributes;
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
        
        [RootQuery]
        public class Queries : GraphQlQuery 
        {
            public Queries(IBus bus)
                : base(bus)
            {
            }
            
            public RootInfo RootInfoQuery(string id) => QueryAsync(new RootInfoQuery(id));
            public RootInfo RootInfoQueryEx(RootInfoQuery query) => QueryAsync(query);
            public Stats StatsQuery() => QueryAsync(new StatsQuery());
            public Stats StatsQueryEx(StatsQuery query) => QueryAsync(query);
            public LastRecord LastRecordQuery(string id) => QueryAsync(new LastRecordQuery(id));
        }
        
        [RootMutation]
        public class Mutations : GraphQlMutation
        {
            public Mutations(IBus bus)
                : base(bus)
            {
            }

            public bool CreateRoot(string name) => CommandAsync(new CreateRoot(name));
            public bool CreateRootEx(CreateRoot command) => CommandAsync(command);
            public bool CreateRecord(string target) => CommandAsync(new CreateRecord(target));
            public bool CreateRecordEx(CreateRecord command) => CommandAsync(command);
            public bool RecordRoot(string target, double recordValue) => CommandAsync(new RecordRoot(target, recordValue));
            public bool RecordRootEx(RecordRoot command) => CommandAsync(command);
        }
    }
}