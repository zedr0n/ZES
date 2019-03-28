using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using NLog;
using ZES.Infrastructure;
using ZES.Infrastructure.Projections;
using ZES.Interfaces;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Tests.TestDomain
{
    public class RootProjection : Projection
    {
        private readonly ConcurrentDictionary<string, long> _createdAt = new ConcurrentDictionary<string, long>();
        
        public RootProjection(IEventStore<IAggregate> eventStore, ILog logger, IMessageQueue messageQueue) : base(eventStore, logger, messageQueue)
        {
            Register<RootCreated>(When);

            eventStore.Streams.Subscribe(async s => await Notify(s));
        }

        public long Get(string id)
        {
            _createdAt.TryGetValue(id, out var createdAt);
            return createdAt;
        }

        private void When(RootCreated e)
        {
            _createdAt[e.RootId] = e.Timestamp;
        }
    }
}