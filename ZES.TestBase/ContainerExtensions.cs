using SimpleInjector;

namespace ZES.TestBase
{
    public static class ContainerExtensions
    {
        public static void UseRemoteStore(this Container c, bool dropAll = true)
        {
            var root = new CompositionRoot();
            root.RegisterRemoteStore(c, dropAll);
        }
        
        public static void UseLocalStore(this Container c, Container baseContainer = null)
        {
            var root = new CompositionRoot();
            root.RegisterLocalStore(c, baseContainer);
        }
    }
}