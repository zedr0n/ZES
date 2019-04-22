using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Streams;
using ZES.Interfaces;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure
{
    public class TimeTraveller : ITimeTraveller
    {
        private readonly ILog _log;
        private readonly ConcurrentDictionary<string, Timeline> _branches = new ConcurrentDictionary<string, Timeline>();
        private readonly Timeline _activeTimeline;
        private readonly IMessageQueue _messageQueue;
        private readonly IStreamStore _streamStore;

        public TimeTraveller(ILog log, ITimeline activeTimeline, IMessageQueue messageQueue, IStreamStore streamStore)
        {
            _log = log;
            _activeTimeline = activeTimeline as Timeline;
            _messageQueue = messageQueue;
            _streamStore = streamStore;
        }
        
        public static string Master { get; } = "master";

        public async Task<ITimeline> Branch(string branchId, long time)
        {
            var timeline = _branches.GetOrAdd(branchId, b => new Timeline { Id = branchId, Now = time });
            if (timeline.Now != time)
            {
                _log.Error($"Branch ${branchId} already exists!");
                throw new InvalidOperationException();    
            }
            
            // update current timeline
            _activeTimeline.Id = timeline.Id;
            _activeTimeline.Now = time;
            
            // copy the events
            await Clone(branchId, time);
            
            // refresh the stream locator
            _messageQueue.Alert(new Alerts.TimelineChanged());
            
            // rebuild all projections
            _messageQueue.Alert(new Alerts.InvalidateProjections());
                
            return _activeTimeline;
        }
        
        public ITimeline Reset()
        {
            _activeTimeline.Id = Master;

            _messageQueue.Alert(new Alerts.TimelineChanged());
            _messageQueue.Alert(new Alerts.InvalidateProjections());

            return _activeTimeline;
        }
        
        // full clone of event store
        // can become really expensive
        // TODO: use links to event ids?
        private async Task Clone(string timeline, long time)
        {
            var page = await _streamStore.ListStreams();
            while (page.StreamIds.Length > 0)
            {
                foreach (var s in page.StreamIds)
                {
                    if (s.StartsWith(timeline))
                        break;
                    var readPage = await _streamStore.ReadStreamForwards(s, 0, 100);
                    while (readPage.Messages.Length > 0)
                    {
                        var messages = new List<NewStreamMessage>();
                        foreach (var m in readPage.Messages)
                        {
                            var timestamp = (long)JObject.Parse(m.JsonMetadata)[nameof(Command.Timestamp)];
                            if (timestamp > time)
                            {
                                readPage = null;
                                break;
                            }

                            var jsonData = await m.GetJsonData();
                            messages.Add(new NewStreamMessage(Guid.NewGuid(), m.Type, jsonData));
                        }
                        
                        var stream = Stream.Branch(s, timeline);
                        var result = await _streamStore.AppendToStream(stream.Key, stream.Version, messages.ToArray());
                        stream.Version = result.CurrentVersion;

                        if (readPage == null)
                            break;
                        
                        readPage = await readPage.ReadNext();
                    }
                }
                
                page = await page.Next();
            }
        }

        private long GetTime(string id)
        {
            if (id == Master)
                return DateTime.UtcNow.Ticks;
            if (_branches.TryGetValue(id, out var timeline))
                return timeline.Now;
            _log.Error($"Branch {id} does not exist!", this);
            throw new InvalidOperationException();
        }
    }
}