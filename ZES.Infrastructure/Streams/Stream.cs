using System;
using SqlStreamStore.Streams;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Streams
{
    /// <inheritdoc />
    public class Stream : IStream
    {
        private readonly string _type;

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

        // public int UpdatedOn { get; set; }

        /// <inheritdoc />
        public string Timeline { get; set; }

        /// <inheritdoc />
        public int Position(int? expectedVersion = null)
        {
            if (expectedVersion == null)
                expectedVersion = Version;

            var version = expectedVersion.Value;
            if (Parent == null) 
                return version;
            
            version -= Parent.Version;
            return version == 0 ? ExpectedVersion.Any : version + ExpectedVersion.EmptyStream;
        }

        /// <inheritdoc />
        public IStream Branch(string timeline, int version)
        {
            if (version <= ExpectedVersion.EmptyStream)
                return null;
            
            return new Stream(Key, version, new Stream(Key, version))
            {
                Timeline = timeline
            };
        }
    }
}