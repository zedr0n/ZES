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
                container.Verify();
                //Bus = container.GetInstance<IBus>();
                return container;
            }
        }
    }
}