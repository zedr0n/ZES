using System.Reflection;
using SimpleInjector;
using ZES.Infrastructure;
using ZES.Infrastructure.GraphQl;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
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
        
        public static void RegisterAllButSagas(Container c)
        {
            c.RegisterAll(Assembly.GetExecutingAssembly(), false);
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

        public static void RegisterAggregates(Container c)
        {
            c.RegisterAggregates(Assembly.GetExecutingAssembly());
        }

        public static void RegisterEvents(Container c)
        {    
            c.RegisterEvents(Assembly.GetExecutingAssembly());   
        }
        
        public class Query : GraphQlQuery 
        {
            public Query(IBus bus)
                : base(bus)
            {
            }
            
            /// <summary>
            /// Gets the root info
            /// </summary>
            /// <param name="id">Root id</param>
            /// <returns>Root info</returns>
            public RootInfo RootInfoQuery(string id) => Resolve(new RootInfoQuery(id));
            public RootInfo RootInfoQueryEx(RootInfoQuery query) => Resolve(query);
            public Stats StatsQuery() => Resolve(new StatsQuery());
            public LastRecord LastRecordQuery(string id) => Resolve(new LastRecordQuery(id));
        }
        
        public class Mutation : GraphQlMutation
        {
            public Mutation(IBus bus, ILog log, IBranchManager manager)
                : base(bus, log, manager)
            {
            }

            public bool CreateRoot(string name) => Resolve(new CreateRoot(name));
            public bool CreateRootEx(CreateRoot command) => Resolve(command);
            public bool CreateRecord(string target) => Resolve(new CreateRecord(target));
            public bool CreateRecordEx(CreateRecord command) => Resolve(command);
            public bool AddRecord(string target, double recordValue) => Resolve(new AddRecord(target, recordValue));
            public bool AddRecordEx(AddRecord command) => Resolve(command);
        }
    }
}