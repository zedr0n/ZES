using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZES.Interfaces.EventStore
{
    /// <summary>
    /// Stream store facade
    /// </summary>
    /// <typeparam name="I">Event sourced type</typeparam>
    public interface IEventStore<I>
        where I : IEventSourced
    {
        /// <summary>
        /// Gets the current streams in the store 
        /// </summary>
        /// <returns>Stream observable</returns>
        /// <value>
        /// Cold observable representing the streams as of call time
        /// </value>
        IObservable<IStream> AllStreams { get; }

        /// <summary>
        /// Gets stream details channel 
        /// </summary>
        /// <value>
        /// Hot observable representing the current streams 
        /// </value>
        IObservable<IStream> Streams { get; }

        /// <summary>
        /// Gets cold observable of all current events in the domain log
        /// </summary>
        /// <value>
        /// Cold observable of all current events in the domain log
        /// </value>
        IObservable<IEvent> Events { get; }
        
        /// <summary>
        /// Read specified number of events from the stream forward from starting version 
        /// </summary>
        /// <param name="stream">Target stream</param>
        /// <param name="start">Starting version for the read</param>
        /// <param name="count">Number of events to read</param>
        /// <returns>Cold observable of read events</returns>
        IObservable<IEvent> ReadStream(IStream stream, int start, int count = -1);
        
        /// <summary>
        /// Append events to stream
        /// </summary>
        /// <param name="stream">Target stream</param>
        /// <param name="events">Events to append</param>
        /// <returns>Task representing the append operation</returns>
        Task AppendToStream(IStream stream, IEnumerable<IEvent> events);
    }
}