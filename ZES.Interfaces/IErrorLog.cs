using System;

namespace ZES.Interfaces
{
    public interface IErrorLog
    {
        void Add(string error, object instance);
        IObservable<IError> Errors { get; }
    }
}