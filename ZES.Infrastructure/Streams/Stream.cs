using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks.Dataflow;
using SqlStreamStore.Streams;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Streams
{
    /// <inheritdoc />
    public class Stream : IStream
    {
        private readonly string _type;
        private readonly List<IStream> _ancestors = new List<IStream>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Stream"/> class.
        /// </summary>
        /// <param name="id">Event sourced id</param>
        /// <param name="type">Event sourced type</param>
        /// <param name="version">Event sourced version</param>
        /// <param name="timeline">Stream timeline</param>
        public Stream(string id, string type, int version, string timeline = "")
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
        public Stream(string key, int version = ExpectedVersion.NoStream, IStream parent = null) 
        {
            var tokens = key.Split(':');
            if (tokens.Length != 3)
                throw new InvalidOperationException();
            
            Timeline = tokens[0];
            _type = tokens[1];
            Id = tokens[2];
            Version = version;
            Parent = parent;
        }

        /// <inheritdoc />
        public string Id { get; }

        /// <inheritdoc />
        public string Key => $"{Timeline}:{_type}:{Id}";

        /// <inheritdoc />
        public int Version { get; set; }

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
                    parent = Parent?.Parent;
                }

                return _ancestors.ToList();
            }
        }

        // public int UpdatedOn { get; set; }

        /// <inheritdoc />
        public string Timeline { get; set; }

        /// <inheritdoc />
        public IStream Copy() => new Stream(Key, Version, Parent);
        
        /// <inheritdoc />
        public int ReadPosition(int version)
        {
            if (Parent != null) 
                version -= Parent.Version + 1;
            
            return version;
        }
        
        /// <inheritdoc />
        public int AppendPosition()
        {
            var version = Version;
            if (Parent == null) 
                return version;
            
            version -= Parent.Version;
            return version == 0 ? ExpectedVersion.Any : version;
        }

        /// <inheritdoc />
        public int Count(int count)
        {
            var parentVersion = Parent?.Version ?? ExpectedVersion.EmptyStream;
            
            count -= parentVersion;
            
            // do not read events ahead of the branch point
            if (count > Version - parentVersion)
                count = Version - parentVersion;
            
            return count;
        }

        /// <inheritdoc />
        public IStream Branch(string timeline, int version)
        {
            var stream = new Stream(Key, version, new Stream(Key, version))
            {
                Timeline = timeline
            };
            if (version == ExpectedVersion.EmptyStream)
                stream.Parent = new Stream(stream.Key, version);

            return stream;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Key}@{Version}";
        }
    }
}