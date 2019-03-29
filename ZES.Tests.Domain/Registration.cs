using SimpleInjector;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain
{
    public class Registration
    {
        public static void RegisterAll(Container c)
        {
           RegisterCommands(c);
           RegisterSagas(c);
           RegisterProjections(c);
        }
        
        public static void RegisterProjections(Container c)
        {
            c.Register<RootProjection>(Lifestyle.Singleton);
            c.Register<StatsProjection>(Lifestyle.Singleton);
            c.Register(typeof(IQueryHandler<,>), new[]
            {
                typeof(CreatedAtHandler),
                typeof(StatsHandler)
            }, Lifestyle.Singleton);
        }
        
        public static void RegisterSagas(Container c)
        {
            c.Register<TestSaga>(Lifestyle.Singleton);
            c.Register<TestSagaHandler>(Lifestyle.Singleton);
        }

        public static void RegisterCommands(Container c)
        {
            c.Register<ICommandHandler<CreateRootCommand>,CreateRootHandler>(Lifestyle.Singleton);
        }
    }
}