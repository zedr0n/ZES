using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NodaTime;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces.EventStore
{
    /// <summary>
    /// Stream store facade
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// Gets stream details channel 
        /// </summary>
        /// <value>
        /// Hot observable representing the current streams 
        /// </value>
        IObservable<IStream> Streams { get; }

        /// <summary>
        /// Reset the database removing all data
        /// </summary>
        /// <returns>Task completes when database is reset</returns>
        Task ResetDatabase();

        /// <summary>
        /// Read specified number of events from the stream forward from starting version 
        /// </summary>
        /// <typeparam name="T">Event or just metadata</typeparam>
        /// <param name="stream">Target stream</param>
        /// <param name="start">Starting version for the read</param>
        /// <param name="count">Number of events to read</param>
        /// <returns>Cold observable of read events</returns>
        IObservable<T> ReadStream<T>(IStream stream, int start, int count = -1)
            where T : class, IEventMetadata;

        /// <summary>
        /// Gets the version of the event with the timestamp before specified
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <param name="timestamp">Up to time</param>
        /// <returns>The last version before timestamp</returns>
        Task<int> GetVersion(IStream stream, Time timestamp);

        /// <summary>
        /// Gets the stream hash for the specified version if any
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <param name="version">Version to query</param>
        /// <returns>The stream hash</returns>
        Task<string> GetHash(IStream stream, int version = int.MaxValue);

        /// <summary>
        /// Append events to stream
        /// </summary>
        /// <param name="stream">Target stream</param>
        /// <param name="events">Events to append</param>
        /// <param name="publish">Publish the events to the message queue</param>
        /// <returns>Task representing the append operation</returns>
        Task AppendToStream(IStream stream, IEnumerable<IEvent> events = null, bool publish = true);

        /// <summary>
        /// Delete the stream
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <returns>Task completes when the stream is deleted</returns>
        Task DeleteStream(IStream stream);

        /// <summary>
        /// Trim the stream removing events after the version
        /// </summary>
        /// <param name="stream">Stream descriptor</param>
        /// <param name="version">Last version to keep</param>
        /// <returns>Task completes when the stream is trimmed</returns>
        Task TrimStream(IStream stream, int version);

        /// <summary>
        /// Gets the current streams in the store 
        /// </summary>
        /// <param name="branch">Branch to filter by</param>
        /// <param name="predicate">Stream id predicate</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Stream observable</returns>
        IObservable<IStream> ListStreams(string branch = null, Func<string, bool> predicate = null, CancellationToken token = default);
    }
    
    /// <summary>
    /// Stream store facade
    /// </summary>
    /// <typeparam name="TEventSourced">Event sourced type</typeparam>
    public interface IEventStore<TEventSourced> : IEventStore
        where TEventSourced : IEventSourced
    {
    }
}