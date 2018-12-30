using System;

namespace ZES.Interfaces
{
    public interface ITimeline
    {
        // id of the alternate timeline we are in
        // at the moment
        // Empty if live
        string TimelineId { get; }
        
        bool Live { get; }
        long Now();
        
        void Set(long date);
        void Reset();
        
        IObservable<T> StopAt<T>(long date, T value);
        void Alternate(Guid timelineId);
    }
}