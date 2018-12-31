using SimpleInjector;
using ZES.CrossCuttingConcerns;
using ZES.Interfaces.Domain;
using ZES.Tests.TestDomain;

namespace ZES.Tests
{
    public class Test
    {
        private readonly object _lock = new object();

        private static CompositionRoot CreateRoot()
        {
            return new CompositionRoot();
        }

        protected Container CreateContainer()
        {
            lock (_lock)
            {
                var container = new Container();
                CreateRoot().ComposeApplication(container);
                container.Register<ICommandHandler<CreateRootCommand>,CreateRootHandler>();

                container.Register<RootProjection>(Lifestyle.Singleton);
                container.Register<TestSaga>(Lifestyle.Singleton);
                container.Register<TestSagaHandler>(Lifestyle.Singleton);
                container.Register(typeof(IQueryHandler<,>), new[]
                {
                    typeof(CreatedAtHandler)
                }, Lifestyle.Singleton);

                container.Verify();
                //Bus = container.GetInstance<IBus>();
                return container;
            }
        }
    }
}