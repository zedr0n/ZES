using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ZES.Interfaces;

namespace ZES.Infrastructure
{
    public class ErrorLog : IErrorLog
    {
        public class Error : IError
        {
            public string ErrorType { get; set; }
            public string Message { get; set; }
            public long? Timestamp { get; set; }

            //public Error() {}
            public Error(Exception error)
            {
                Message = error.Message;
                ErrorType = error.GetType().Name;
                Timestamp = DateTime.UtcNow.Ticks;
            }
        }
        
        private readonly ILog _log;
        private readonly BehaviorSubject<IError> _errors = new BehaviorSubject<IError>(null); 

        public ErrorLog(ILog log)
        {
            _log = log;
        }

        public void Add(Exception error)
        {
            _log.Error(error.Message, error.StackTrace.Split(new[] {"in", "at","(",")","[","]"},StringSplitOptions.RemoveEmptyEntries)[1]+ ' ');
            _errors.OnNext(new Error(error));
        }

        public IObservable<IError> Errors => _errors.AsObservable();
    }
}