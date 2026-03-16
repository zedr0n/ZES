using SimpleInjector;

namespace ZES.TestBase
{
    /// <summary>
    /// Provides extension methods for configuring a SimpleInjector <see cref="Container"/>
    /// with different types of storage solutions, including remote and local stores.
    /// </summary>
    public static class ContainerExtensions
    {
        /// <summary>
        /// Configures the specified SimpleInjector <see cref="Container"/> to use a remote store as its storage solution.
        /// </summary>
        /// <param name="c">The SimpleInjector <see cref="Container"/> to be configured.</param>
        /// <param name="dropAll">
        /// A boolean value indicating whether the contents of the remote store should be cleared before use.
        /// Defaults to <see langword="true"/>.
        /// </param>
        public static void UseRemoteStore(this Container c, bool dropAll = true)
        {
            var root = new CompositionRoot();
            root.RegisterRemoteStore(c, dropAll);
        }

        /// <summary>
        /// Configures the specified SimpleInjector <see cref="Container"/> to use a local store as its storage solution.
        /// </summary>
        /// <param name="c">The SimpleInjector <see cref="Container"/> to be configured.</param>
        /// <param name="baseContainer">
        /// An optional <see cref="Container"/> that serves as the base container for resolving dependencies.
        /// If not provided, no base container will be used.
        /// </param>
        public static void UseLocalStore(this Container c, Container baseContainer = null)
        {
            var root = new CompositionRoot();
            root.RegisterLocalStore(c, baseContainer);
        }
    }
}