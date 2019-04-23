using System;
using SqlStreamStore.Streams;
using ZES.Interfaces.EventStore;

namespace ZES.Infrastructure.Streams
{
    /// <inheritdoc />
    public class Stream : IStream
    {
        private readonly string _id;
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
            _id = id;
            _type = type;
            Version = version;
            Timeline = timeline;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Stream"/> class.
        /// </summary>
        /// <param name="key">Stream key</param>
        /// <param name="version">Event sourced version</param>
        /// <exception cref="InvalidOperationException">if key is not of expected format</exception>
        public Stream(string key, int version = ExpectedVersion.NoStream)
        {
            var tokens = key.Split(':');
            if (tokens.Length != 3)
                throw new InvalidOperationException();
            
            Timeline = tokens[0];
            _type = tokens[1];
            _id = tokens[2];
            Version = version;
        }

        /// <inheritdoc />
        public string Key => $"{Timeline}:{_type}:{_id}";

        /// <inheritdoc />
        public int Version { get; set; }
        
        // public int UpdatedOn { get; set; }

        /// <inheritdoc />
        public string Timeline { get; set; }
        
        /// <summary>
        /// Creates the stream descriptor for the parallel branch
        /// </summary>
        /// <param name="key">Stream identifier</param>
        /// <param name="timeline">Timeline of the stream</param>
        /// <returns>New stream object</returns>
        public static Stream Branch(string key, string timeline)
        {
            return new Stream(key) { Timeline = timeline };
        }
    }
}