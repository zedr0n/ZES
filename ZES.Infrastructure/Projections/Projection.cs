using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;
using ZES.Infrastructure.Alerts;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Pipes;

namespace ZES.Infrastructure.Projections
{
    public class Projection<TState> : IProjection<TState>
        where TState : new()
    {
        private readonly IMessageQueue _messageQueue;

        private readonly IEventStore<IAggregate> _eventStore;
        private readonly ConcurrentDictionary<string, int> _streams = new ConcurrentDictionary<string, int>();

        private CancellationTokenSource _streamSource;
        private CancellationTokenSource _rebuildSource;

        protected Projection(IEventStore<IAggregate> eventStore, ILog log, IMessageQueue messageQueue)
        {
            _eventStore = eventStore;
            Log = log;
            _messageQueue = messageQueue;

            _streamSource = new CancellationTokenSource();
            _rebuildSource = new CancellationTokenSource();

            OnInit();
        }

        public Task Complete { get; private set; }
        public TState State { get; protected set; } = new TState();
        public Dictionary<Type, Func<IEvent, TState, TState>> Handlers { get; } = new Dictionary<Type, Func<IEvent, TState, TState>>();

        protected ILog Log { get; }

        public async Task Start(bool rebuild = true)
        {
            if (rebuild)
                await Rebuild();
            else
                ListenToStreams();
            _messageQueue.Alerts.OfType<InvalidateProjections>().Subscribe(async s => await Rebuild());
        }

        protected virtual void OnInit()
        {
            Start();
        }

        protected void Register<TEvent>(Func<TEvent, TState, TState> when)
            where TEvent : class
        {
            Handlers.Add(typeof(TEvent), (e, s) => when(e as TEvent, s));
        }

        protected void Register(Type tEvent, Func<IEvent, TState, TState> when)
        {
            Handlers.Add(tEvent, when);
        }

        private void ListenToStreams()
        {
            Log.Trace(string.Empty, this);
            _streamSource.Cancel();
            _streamSource = new CancellationTokenSource();
            var streamFlow = new StreamFlow(_eventStore, _streams, When, _streamSource);
            _eventStore.Streams.Subscribe(async s => await streamFlow.InputBlock.SendAsync(s), _streamSource.Token);
        }

        private async Task Rebuild()
        {
            Log.Trace(string.Empty, this);

            _streamSource.Cancel();
            _rebuildSource.Cancel();

            lock (State)
                State = new TState();

            _rebuildSource = new CancellationTokenSource();
            var eventFlow = new EventFlow(When, _rebuildSource);
            await eventFlow.SendAsync(_eventStore.Events);
            Complete = eventFlow.CompletionTask;
            await eventFlow.CompletionTask;
            ListenToStreams();
        }

        private void When(IEvent e)
        {
            if (_rebuildSource.IsCancellationRequested)
            {
                Log.Debug("Cancellation requested!");
                return;
            }

            Log.Trace(string.Empty, this);
            if (e == null)
                return;

            if (!Handlers.TryGetValue(e.GetType(), out var handler))
                return;

            State = handler(e, State);
            _streams[e.Stream] = e.Version;
        }

        private class EventFlow : Dataflow<IObservable<IEvent>>
        {
            private readonly BufferBlock<IObservable<IEvent>> _inputBlock;

            public EventFlow(Action<IEvent> when, CancellationTokenSource tokenSource)
                : base(DataflowOptions.Default)
            {
                _inputBlock = new BufferBlock<IObservable<IEvent>>();
                var updateBlock = new ActionBlock<IObservable<IEvent>>(
                    e => e.Finally(_inputBlock.Complete).Subscribe(when, tokenSource.Token),
                    new ExecutionDataflowBlockOptions { CancellationToken = tokenSource.Token });

                _inputBlock.LinkTo(updateBlock, new DataflowLinkOptions { PropagateCompletion = true });

                RegisterChild(_inputBlock);
                RegisterChild(updateBlock);
            }

            public override ITargetBlock<IObservable<IEvent>> InputBlock => _inputBlock;
        }

        private class StreamFlow : Dataflow<IStream>
        {
            private readonly BufferBlock<IStream> _inputBlock;

            public StreamFlow(
                IEventStore<IAggregate> eventStore,
                ConcurrentDictionary<string, int> streams,
                Action<IEvent> when,
                CancellationTokenSource tokenSource)
                : base(DataflowOptions.Default)
            {
                _inputBlock = new BufferBlock<IStream>();

                int LocalVersion(IStream s) => streams.GetOrAdd(s.Key, -1);

                var readBlock = new TransformBlock<IStream, IObservable<IEvent>>(
                    s => eventStore.ReadStream(s, LocalVersion(s) + 1),
                    new ExecutionDataflowBlockOptions { CancellationToken = tokenSource.Token });

                var updateBlock = new ActionBlock<IObservable<IEvent>>(
                    e => e.Subscribe(when, tokenSource.Token),
                    new ExecutionDataflowBlockOptions { CancellationToken = tokenSource.Token, MaxDegreeOfParallelism = 8 });

                _inputBlock.LinkTo(
                    readBlock,
                    new DataflowLinkOptions { PropagateCompletion = true },
                    s => LocalVersion(s) < s.Version);
                readBlock.LinkTo(updateBlock);

                RegisterChild(_inputBlock);
                RegisterChild(readBlock);
                RegisterChild(updateBlock);
            }

            public override ITargetBlock<IStream> InputBlock => _inputBlock;
        }
    }
}