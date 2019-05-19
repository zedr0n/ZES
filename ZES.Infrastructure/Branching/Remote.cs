using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SqlStreamStore;
using SqlStreamStore.Streams;
using ZES.Infrastructure.Attributes;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;

namespace ZES.Infrastructure.Branching
{
    public class Remote<T> : IRemote<T> 
        where T : IEventSourced
    {
        private readonly IStreamStore _localStore;
        private readonly IStreamStore _remoteStore;
        private readonly ILog _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="Remote{T}"/> class.
        /// </summary>
        /// <param name="localStore">Local stream store</param>
        /// <param name="remoteStore">Target remote</param>
        /// <param name="log">Log helper</param>
        public Remote(IStreamStore localStore, [Remote] IStreamStore remoteStore, ILog log)
        {
            _localStore = localStore;
            _remoteStore = remoteStore;
            _log = log;
        }

        /// <inheritdoc />
        public async Task<PushResult> Push(string branchId)
        {
            var pushResult = new PushResult();
            
            var valid = await ValidatePush(branchId);
            if (!valid)
            {
                _log.Warn($"Push of {branchId} to remote aborted, can't fast-forward", this);
                return pushResult;
            }

            var page = await _localStore.ListStreams();
            while (page.StreamIds.Length > 0)
            {
                foreach (var s in page.StreamIds.Where(x => !x.StartsWith("$")))
                {
                    var localPosition = await _localStore.LastPosition(s);
                    var remotePosition = await _remoteStore.LastPosition(s);

                    if (localPosition < remotePosition)
                        throw new InvalidOperationException( $"Remote({remotePosition}) is ahead of local({localPosition}) for stream {s}");

                    if (localPosition == remotePosition)
                        continue;

                    var eventPage = await _localStore.ReadStreamForwards(s, remotePosition + 1, Configuration.BatchSize);
                    while (eventPage.Messages.Length > 0)
                    {
                        var appendMessages = new List<NewStreamMessage>();
                        foreach (var m in eventPage.Messages)
                        {
                            var payload = await m.GetJsonData(); 
                            var message = new NewStreamMessage(m.MessageId, m.Type, payload, m.JsonMetadata);
                            appendMessages.Append(message);
                        }

                        var result = await _localStore.AppendToStream(s, remotePosition, appendMessages.ToArray());
                        pushResult.NumberOfMessages += result.CurrentVersion - remotePosition; 
                        eventPage = await eventPage.ReadNext();
                    }

                    pushResult.NumberOfStreams++;
                }

                page = await page.Next();
            }

            pushResult.Status = PushResult.PushResultStatus.Success;
            
            _log.Info($"Pushed {pushResult.NumberOfMessages} objects to {pushResult.NumberOfStreams} streams");
            return pushResult;
        }

        /// <inheritdoc />
        public Task Pull(string branchId)
        {
            throw new System.NotImplementedException();
        }

        private async Task<bool> ValidatePush(string branchId)
        {
            return await Validate(_localStore, _remoteStore, branchId);
        }
        
        private async Task<bool> ValidatePull(string branchId)
        {
            return await Validate(_remoteStore, _localStore, branchId);
        } 
        
        // TODO: check if parent branches exist
        private async Task<bool> Validate(
            IStreamStore from,
            IStreamStore to,
            string branchId,
            bool failOnNoStream = false )
        {
            var page = await from.ListStreams();
            while (page.StreamIds.Length > 0)
            {
                foreach (var s in page.StreamIds.Where(x => !x.StartsWith("$")))
                {
                    var source = await from.GetStream(s);
                    if (source.Timeline != branchId)
                        continue;

                    var target = await to.GetStream(s);
                    if (target == null) // stream exists but some of the ancestors do not
                        return false;

                    if (failOnNoStream && target.Version == ExpectedVersion.NoStream)
                    {
                        _log.Warn($"Stream {s} does not exist on target, probably due to missing ancestors", this);
                        return false;
                    }

                    if (source.Version < target.Version)
                    {
                        _log.Warn($"Target({target.Version}) is ahead of source({source.Version}) for stream {s}", this);
                        return false;
                    }

                    var parent = source.Parent;
                    while (parent != null && parent.Version > ExpectedVersion.EmptyStream)
                    {
                        if (!await Validate(from, to, parent.Timeline, true))
                            return false;
                        
                        parent = parent.Parent;
                    }
                }

                page = await page.Next();
            }

            return true;
        }
    }
}