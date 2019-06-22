using System.Linq;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <summary>
    /// Command handler extensions
    /// </summary>
    public static class HandlerExtensions
    {
        /// <summary>
        /// Get aggregate root type 
        /// </summary>
        /// <param name="handler">Command handler</param>
        /// <typeparam name="T">Command type</typeparam>
        /// <returns>Aggregate root type</returns>
        public static string RootType<T>(this ICommandHandler<T> handler) 
            where T : ICommand
        {
            var t = handler.GetType()
                .GetInterfaces()
                .SelectMany(i => i.GetGenericArguments())
                .SingleOrDefault(x => x.GetInterfaces().Contains(typeof(IAggregate)));

            return t?.Name;
        }
    }
}