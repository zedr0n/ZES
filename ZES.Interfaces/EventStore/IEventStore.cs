using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Interfaces.EventStore
{
    public interface IEventStore<I> where I : IEventSourced
    {
        /// <summary>
        /// Trigger info when streams finish loading
        /// </summary>
        IObservable<bool> StreamsBatched { get; }
        /// <summary>
        /// Stream details channel 
        /// </summary>
        IObservable<IStream> Streams { get; }       
        /// <summary>
        /// Cold observable of all current events in the domain log
        /// </summary>
        IObservable<IEvent> Events { get; }
        /// <summary>
        /// Read specified number of events from the stream forward from starting version 
        /// </summary>
        /// <param name="stream">Target stream</param>
        /// <param name="start">Starting version for the read</param>
        /// <param name="count">Number of events to read</param>
        /// <returns></returns>
        Task<IEnumerable<IEvent>> ReadStream(IStream stream, int start, int count = -1);
        /// <summary>
        /// Append events to stream
        /// </summary>
        /// <param name="stream">Target stream</param>
        /// <param name="events">Events to append</param>
        Task AppendToStream(IStream stream, IEnumerable<IEvent> events);
        /// <summary>
        /// Save the command to command log
        /// </summary>
        /// <param name="command"></param>
        Task AppendCommand(ICommand command);
    }
}