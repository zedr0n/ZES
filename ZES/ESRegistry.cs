using System;
using System.Collections.Generic;
using System.Linq;
using SimpleInjector;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES
{
    /// <inheritdoc />
    public class EsRegistry : IEsRegistry
    {
        private readonly Lazy<Dictionary<string, Type>> _types;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="EsRegistry"/> class.
        /// </summary>
        /// <param name="container">SimpleInjector container</param>
        public EsRegistry(Container container)
        {
            _types = new Lazy<Dictionary<string, Type>>(() =>
            {
                var dict = new Dictionary<string, Type>();
                foreach (var es in container.GetAllInstances<IEventSourced>())
                {
                    if (!dict.ContainsKey(es.GetType().Name))
                        dict.Add(es.GetType().Name, es.GetType());
                }

                return dict;
            });
        }
        
        private Dictionary<string, Type> Types => _types.Value;

        /// <inheritdoc />
        public Type GetType(string type)
        {
            if (Types.ContainsKey(type))
                return Types[type];

            return null;
        }
    }
}