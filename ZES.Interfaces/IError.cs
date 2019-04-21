using System;

namespace ZES.Interfaces
{
    public interface IError
    {
        string ErrorType { get; }
        string Message { get; }
        long? Timestamp { get; }
    }
}