using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    /// <inheritdoc />
    public class DefaultProjection<TState> : GlobalProjection<TState>
        where TState : IState, new()
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultProjection{TState}"/> class.
        /// </summary>
        /// <param name="eventStore">Event store</param>
        /// <param name="log">Log service</param>
        /// <param name="activeTimeline">Branch</param>
        /// <param name="messageQueue">Message queue</param>
        /// <param name="streamLocator">Stream locator</param>
        /// <param name="handlers">Event handlers</param>
        public DefaultProjection(IEventStore<IAggregate> eventStore, ILog log, ITimeline activeTimeline, IMessageQueue messageQueue, IStreamLocator streamLocator, IEnumerable<IProjectionHandler<TState>> handlers)
            : base(eventStore, log, activeTimeline, messageQueue, streamLocator)
        {
            State = new TState();
            foreach (var h in handlers)
            {
                var tEvents =
                    h.GetType().GetInterfaces().Where(i => i.GenericTypeArguments.Length > 1)
                        .Select(i => i.GenericTypeArguments[1]).ToList();

                // register remainder using reflection
                var otherMethods = h.GetType().GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly |
                                                          BindingFlags.InvokeMethod | BindingFlags.Instance)
                    .Where(m => m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase));

                foreach (var method in otherMethods)
                {
                    var tEvent = method.GetParameters().First().ParameterType;
                    if (tEvent != typeof(IEvent) && !tEvents.Contains(tEvent))
                    {
                        // tEvents.Add(tEvent);
                        // var func = method.CreateDelegate(typeof(Func<,,,>).MakeGenericType(h.GetType(), tEvent, typeof(TState), typeof(TState)));
                        var invoker = EfficientInvoker.ForMethodInfo(h.GetType(), method);
                        if (invoker != null)
                            Register(tEvent, (e, state) => (TState)invoker.Invoke(h, e, state));
                    }
                }
                
                foreach (var tEvent in tEvents)
                    Register(tEvent, h.Handle);
            }
        }
    }
}