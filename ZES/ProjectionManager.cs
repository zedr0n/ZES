using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SimpleInjector;
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
        public IProjection<TState> GetProjection<TState>(string id = "")
            where TState : IState
        {
            var descriptor = new Descriptor<TState>(id);
            var projection = _projections.GetOrAdd(
                descriptor,
                d => CreateProjection<TState>(id)) as IProjection<TState>;

            return projection;
        }

        /// <inheritdoc />
        public IHistoricalProjection<TState> GetHistoricalProjection<TState>(string id = "")
            where TState : IState, new()
        {
            id = id.ToUpper().Replace(' '.ToString(), "_");
            var historicalProjection = _container.GetInstance<IHistoricalProjection<TState>>();
            if (id != string.Empty)
            {
                historicalProjection.Predicate = s => s.Id == id;
                historicalProjection.StreamIdPredicate = s => s.Contains(id);
            }

            return historicalProjection;
        }

        private IProjection<TState> CreateProjection<TState>(string id)
            where TState : IState
        {
            id = id.ToUpper().Replace(' '.ToString(), "_");
            var p = _container.GetInstance<IProjection<TState>>();
            if (typeof(TState).GetInterfaces().Contains(typeof(ISingleState)) && id == string.Empty)
                return null;

            if (id != string.Empty)
            {
                p.Predicate = s => s.Id == id;
                p.StreamIdPredicate = s => s.Contains(id);
            }

            return p;
        }
        
        private abstract class Descriptor : ValueObject
        {
            protected Descriptor(string streamId)
            {
                StreamId = streamId;
            }

            public abstract Type StateType { get; }
            public string StreamId { get; }
        }

        /// <inheritdoc />
        private class Descriptor<TState> : Descriptor
            where TState : IState
        {
            public Descriptor(string streamId = "") 
                : base(streamId)
            {
            }
            
            public override Type StateType => typeof(TState);

            /// <inheritdoc />
            protected override IEnumerable<object> GetAtomicValues()
            {
                yield return StreamId;
            }
        }
    }
}