using System;
using SimpleInjector;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES
{
    /// <inheritdoc />
    public class CommandRegistry : ICommandRegistry
    {
        private readonly ILog _log;
        private readonly Container _container;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandRegistry"/> class.
        /// </summary>
        /// <param name="container">SimpleInjector container</param>
        /// <param name="log">Log service</param>
        public CommandRegistry(Container container, ILog log)
        {
            _container = container;
            _log = log;
        }

        /// <inheritdoc />
        public ICommandHandler GetHandler(ICommand command) 
        {
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
            var handler = (ICommandHandler)GetInstance(handlerType);
            return handler;
        }
        
        private object GetInstance(Type type)
        {
            try
            {
                var instance = _container.GetInstance(type);
                return instance;
            }
            catch (Exception e)
            {
                _log.Errors.Add(e); 
                if (e is ActivationException)
                    return null;
                throw;
            }
        }
    }
}