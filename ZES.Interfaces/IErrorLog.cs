using System;

namespace ZES.Interfaces
{
    public interface IErrorLog
    {
        void Add(Exception error, object instance);
        IObservable<IError> Errors { get; }
    }
}