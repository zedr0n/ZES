using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZES.Interfaces.EventStore
{
    public interface IEventStore<I>
        where I : IEventSourced
    {
        /// <summary>
        /// Gets currently stored streams
        /// </summary>
        /// <returns>Stream observable</returns>
        IObservable<IStream> AllStreams { get; }
        
        /// <summary>
        /// Gets stream details channel 
        /// </summary>
        IObservable<IStream> Streams { get; }       
        
        /// <summary>
        /// Gets cold observable of all current events in the domain log
        /// </summary>
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