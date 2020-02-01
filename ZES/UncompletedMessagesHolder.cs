using System;
using System.Collections.Concurrent;
using ZES.Infrastructure.Utils;

namespace ZES
{
    public class UncompletedMessagesHolder : StateHolder<UncompletedMessages, UncompletedMessagesBuilder>
    {
        public IObservable<int> UncompletedMessages(string timeline)
        {
            return Project(x =>
            {
                if (x.Count.TryGetValue(timeline, out var count))
                    return count;
                return 0;
            });
        }
    }

    public struct UncompletedMessages
    {
        public ConcurrentDictionary<string, int> Count;

        public UncompletedMessages(ConcurrentDictionary<string, int> count)
        {
           Count = new ConcurrentDictionary<string, int>(count); 
        }
    }

    public struct UncompletedMessagesBuilder : IHeldStateBuilder<UncompletedMessages, UncompletedMessagesBuilder>
    {
        public ConcurrentDictionary<string, int> Count { get; private set; }
        
        public void InitializeFrom(UncompletedMessages state)
        {
            Count = new ConcurrentDictionary<string, int>(state.Count);
        }

        public UncompletedMessages Build()
        {
           return new UncompletedMessages(Count); 
        }

        public UncompletedMessages DefaultState()
        {
            return new UncompletedMessages(new ConcurrentDictionary<string, int>());
        }
    }
}