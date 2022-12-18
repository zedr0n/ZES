using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;
using ZES.Infrastructure.Domain;
using ZES.Interfaces;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.EventStore
{
    /// <inheritdoc />
    public class Stream : IStream
    {
        private readonly string _type;
        private readonly List<IStream> _ancestors = new List<IStream>();
        private int _version;

        /// <summary>
        /// Initializes a new instance of the <see cref="Stream"/> class.
        /// </summary>
        /// <param name="id">Event sourced id</param>
        /// <param name="type">Event sourced type</param>
        /// <param name="version">Event sourced version</param>
        /// <param name="timeline">Stream timeline</param>
        public Stream(string id, string type, int version, string timeline)
        {
            Id = id;
            _type = type;
            Version = version;
            Timeline = timeline;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Stream"/> class.
        /// </summary>
        /// <param name="key">Stream key</param>
        /// <param name="version">Event sourced version</param>
        /// <param name="parent">Parent stream info</param>
        /// <exception cref="InvalidOperationException">if key is not of expected format</exception>
        public Stream(string key, int version = int.MinValue, IStream parent = null)
        {
            if (version == int.MinValue)
                version = ExpectedVersion.NoStream;
            
            var tokens = key.Split(':');
            if (tokens.Length != 3)
                throw new InvalidOperationException();
            
            Timeline = tokens[0];
            _type = tokens[1];
            Id = tokens[2];
            Version = version;
            Parent = parent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Stream"/> class.
        /// </summary>
        /// <param name="es">Event sourced instance</param>
        /// <param name="timeline">Target timeline</param>
        public Stream(IEventSourced es, string timeline = "") 
            : this(es.Id, es.GetType().Name, ExpectedVersion.NoStream, timeline)
        {
        }

        /// <inheritdoc />
        public bool IsSaga => _type.ToUpper().Contains(nameof(Saga).ToUpper());

        /// <inheritdoc />
        public string Id { get; }

        /// <inheritdoc />
        public string Key => $"{Timeline}:{_type}:{Id.Replace(' '.ToString(), "_")}";

        /// <inheritdoc />
        public int Version
        {
            get => _version;
            set => _version = value;
        }

        /// <inheritdoc />
        public Time SnapshotTimestamp { get; set; } = Time.Default;

        /// <inheritdoc />
        public int SnapshotVersion { get; set; }

        /// <inheritdoc />
        public string Type => _type;

        /// <inheritdoc />
        public IStream Parent { get; set; }
        
        /// <inheritdoc />
        public IEnumerable<IStream> Ancestors
        {
            get
            {
                if (_ancestors.Count > 0 || Parent == null)
                    return _ancestors.ToList();
                
                var parent = Parent;
                while (parent != null && parent.Version > ExpectedVersion.NoStream)
                {
                    _ancestors.Add(parent);
                    parent = parent.Parent;
                }

                return _ancestors.ToList();
            }
        }
        
        /// <inheritdoc />
        public int DeletedCount { get; private set; }

        /// <inheritdoc />
        public string Timeline { get; set; }

        /// <inheritdoc />
        public IStream Copy() => new Stream(Key, Version, Parent?.Copy())
        {
            SnapshotTimestamp = SnapshotTimestamp,
            SnapshotVersion = SnapshotVersion,
            DeletedCount = DeletedCount,
        };
        
        /// <inheritdoc />
        public int ReadPosition(int version)
        {
            if (Parent != null) 
                version -= Parent.Version + 1;

            return version < 0 ? 0 : version;
        }

        /// <inheritdoc />
        public int Count(int start, int count = -1)
        {
            if (count == 0)
                return 0;
            
            var end = int.MaxValue;
            if (count > 0 && count < int.MaxValue)
                end = start + count - 1;

            var lastVersion = Version;
            var firstVersion = Parent?.Version + 1 ?? 0;  
            
            // cloned streams have Version = Parent.Version but no actual events
            if (Parent?.Version == Version)
                return 0; 

            if (start > lastVersion || end < firstVersion)
                return 0;

            if (end > lastVersion)
                end = lastVersion;

            if (start < firstVersion)
                start = firstVersion;

            if (end < start)
                return 0;
            
            return end - start + 1;
        }

        /// <inheritdoc />
        public void AddDeleted(int count)
        {
            DeletedCount += count;
        }

        /// <inheritdoc />
        public int AppendPosition()
        {
            var version = Version;
            version += DeletedCount;
            if (Parent == null || Parent?.Version <= ExpectedVersion.NoStream) 
                return version;
            
            version -= Parent.Version + 1;
            return version < 0 ? ExpectedVersion.Any : version;
        }

        /// <inheritdoc />
        public IStream Branch(string timeline, int version)
        {
            if (Timeline == timeline)
                return new Stream(Key, version, Parent);

            var parentVersion = version >= Version ? Version : version;
            if (parentVersion < 0)
                parentVersion = ExpectedVersion.EmptyStream;

            var stream = new Stream(
                Key,
                version)
            {
                SnapshotTimestamp = SnapshotVersion <= version ? SnapshotTimestamp : Time.MaxValue, 
                SnapshotVersion = SnapshotVersion <= version ? SnapshotVersion : 0,
                Timeline = timeline,
            };

            if ( (Parent == null && parentVersion > ExpectedVersion.EmptyStream ) || ( Parent != null && parentVersion > Parent.Version) )
            {
                stream.Parent = new Stream(Key, parentVersion, Parent)
                {
                    SnapshotTimestamp = SnapshotVersion <= version ? SnapshotTimestamp : Time.MaxValue,
                    SnapshotVersion = SnapshotVersion <= version ? SnapshotVersion : 0,
                };
            }
            else if (Parent != null)
            {
                stream.Parent = Parent.Copy();
                if (Parent.Version > version)
                    stream.Version = Parent.Version;
            }
            
            return stream;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Key}@{Version}";
        }

        /// <inheritdoc />
        public class StreamComparer : IEqualityComparer<IStream>
        {
            /// <inheritdoc />
            public bool Equals(IStream x, IStream y)
            {
                if (x == y)
                    return true;
                
                if (x == null || y == null)
                    return false;

                return x.Key == y.Key && x.Version == y.Version;
            }

            /// <inheritdoc />
            public int GetHashCode(IStream obj) => obj.Key.GetHashCode() ^ obj.Version.GetHashCode();
        }

        /// <inheritdoc />
        public class BranchComparer : IEqualityComparer<IStream>
        {
            /// <inheritdoc />
            public bool Equals(IStream x, IStream y)
            {
                if (x == y)
                    return true;
                
                if (x == null || y == null)
                    return false;

                return x.Id == y.Id && x.Type == y.Type;
            }

            /// <inheritdoc />
            public int GetHashCode(IStream obj) => obj.Id.GetHashCode() ^ obj.Type.GetHashCode();
        }
    }
}