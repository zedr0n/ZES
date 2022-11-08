using SimpleInjector;

namespace ZES.Tests
{
    public static class ContainerExtensions
    {
        public static void UseRemoteStore(this Container c, bool dropAll = true)
        {
            var root = new CompositionRoot();
            root.RegisterRemoteStore(c, dropAll);
        }
        
        public static void UseLocalStore(this Container c, bool useGenericRemote = false, Container baseContainer = null)
        {
            var root = new CompositionRoot();
            root.RegisterLocalStore(c, useGenericRemote, baseContainer);
        }
    }
}