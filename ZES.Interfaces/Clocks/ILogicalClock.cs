﻿namespace ZES.Interfaces.Clocks
{
    /// <summary>
    /// Common interface for logical ( hybrid ) clocks
    /// </summary>
    public interface ILogicalClock : IClock
    {
        /// <summary>
        /// Update the clock for send/local event
        /// </summary>
        void Tick();

        /// <summary>
        /// Update the clock for received event
        /// </summary>
        /// <param name="received">Received message clock</param>
        void Receive(Time received);
    }
}