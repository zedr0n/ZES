using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SimpleInjector;
using SqlStreamStore.Streams;
using ZES.Infrastructure;
using ZES.Interfaces.Domain;

namespace ZES
{
    /// <inheritdoc />
    public class ProjectionManager : IProjectionManager
    {
        private readonly Container _container;
        private readonly ConcurrentDictionary<Descriptor, IProjection> _projections = new ConcurrentDictionary<Descriptor, IProjection>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectionManager"/> class.
        /// </summary>
        /// <param name="container">SimpleInjector DI container</param>
        public ProjectionManager(Container container)
        {
            _container = container;
        }

        /// <inheritdoc />
        public IProjection<TState> GetProjection<TState>(string id = "", string timeline = "")
            where TState : IState
        {
            var descriptor = new Descriptor<TState>(id, timeline);
            var projection = _projections.GetOrAdd(
                descriptor,
                d => CreateProjection<TState>(id, timeline)) as IProjection<TState>;

            return projection;
        }

        /// <inheritdoc />
        public IHistoricalProjection<TState> GetHistoricalProjection<TState>(string id = "")
            where TState : IState, new()
        {
            id = id.Replace(' '.ToString(), "_");
            var historicalProjection = _container.GetInstance<IHistoricalProjection<TState>>();
            if (id != string.Empty)
            {
                historicalProjection.Predicate = s => s.Id == id;
                historicalProjection.StreamIdPredicate = s => s.Contains(id);
            }

            return historicalProjection;
        }

        private IProjection<TState> CreateProjection<TState>(string id, string timeline)
            where TState : IState
        {
            id = id.Replace(' '.ToString(), "_");
            var p = _container.GetInstance<IProjection<TState>>();
            if (typeof(TState).GetInterfaces().Contains(typeof(ISingleState)) && id == string.Empty)
                return null;

            if (id != string.Empty)
            {
                p.Predicate = s => s.Id == id;
                p.StreamIdPredicate = s => s.Contains(id);
            }

            if (timeline != string.Empty)
                p.Timeline = timeline;

            return p;
        }
        
        private abstract record Descriptor(string StreamId, string Timeline) 
        {
            public abstract Type StateType { get; }
        }

        /// <inheritdoc />
        private record Descriptor<TState>(string StreamId, string Timeline) : Descriptor(StreamId, Timeline)
            where TState : IState
        {
            public override Type StateType => typeof(TState);
        }
    }
}