using System;

namespace ZES.Interfaces
{
    public interface IErrorLog
    {
        IObservable<IError> Errors { get; }
        
        void Add(Exception error);
    }
}