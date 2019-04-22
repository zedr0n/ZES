using System;

namespace ZES.Interfaces
{
    public interface IErrorLog
    {
        void Add(Exception error);
        IObservable<IError> Errors { get; }
    }
}