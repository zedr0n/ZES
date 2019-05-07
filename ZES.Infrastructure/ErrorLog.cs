using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ZES.Interfaces;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class ErrorLog : IErrorLog
    {
        private readonly ILog _log;
        private readonly BehaviorSubject<IError> _errors = new BehaviorSubject<IError>(null);

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorLog"/> class.
        /// </summary>
        /// <param name="log">Application log</param>
        public ErrorLog(ILog log)
        {
            _log = log;
            log.Errors = this;
        }

        /// <inheritdoc />
        public IObservable<IError> Errors => _errors.AsObservable();

        /// <inheritdoc />
        public void Add(Exception error)
        {
            if (error == null)
                return;
            _log.Error(error.Message, error.StackTrace?.Split(new[] { "in", "at", "(", ")", "[", "]" }, StringSplitOptions.RemoveEmptyEntries)[1] + ' ');
            _errors.OnNext(new Error(error));
        }

        /// <inheritdoc />
        public class Error : IError
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Error"/> class.
            /// </summary>
            /// <param name="error">Caught exception</param>
            public Error(Exception error)
            {
                Message = error.Message;
                ErrorType = error.GetType().Name;
                Timestamp = DateTime.UtcNow.Ticks;
            }

            /// <inheritdoc />
            public string ErrorType { get; set; }

            /// <inheritdoc />
            public string Message { get; set; }

            /// <inheritdoc />
            public long? Timestamp { get; set; }
        }
    }
}