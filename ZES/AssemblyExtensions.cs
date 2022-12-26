using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ZES
{
    /// <summary>
    /// Assembly extensions
    /// </summary>
    public static class AssemblyExtensions
    {
        /// <summary>
        /// Gets all referenced assemblies
        /// </summary>
        /// <param name="rootAssembly">Root assembly</param>
        /// <returns>All referenced assemblies</returns>
        public static IEnumerable<Assembly> GetAllAssemblies(this Assembly rootAssembly)
        {
            var returnAssemblies = new List<Assembly>();
            var loadedAssemblies = new HashSet<string>();
            var assembliesToCheck = new Queue<Assembly>();

            assembliesToCheck.Enqueue(rootAssembly);

            while (assembliesToCheck.Any())
            {
                var assemblyToCheck = assembliesToCheck.Dequeue();

                foreach (var reference in assemblyToCheck.GetReferencedAssemblies())
                {
                    var fullName = reference.FullName;
                    if (!loadedAssemblies.Contains(fullName))
                    {
                        var assembly = Assembly.Load(reference);
                        assembliesToCheck.Enqueue(assembly);
                        loadedAssemblies.Add(fullName);
                        returnAssemblies.Add(assembly);
                    }
                }
            }

            return returnAssemblies;
        }
    }
}