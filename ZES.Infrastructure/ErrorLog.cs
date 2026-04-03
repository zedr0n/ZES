using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Extensions;
using ZES.Interfaces;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Infrastructure;

namespace ZES.Infrastructure
{
    /// <inheritdoc />
    public class ErrorLog : IErrorLog
    {
        private readonly ILog _log;
        private readonly BehaviorSubject<IError> _errors = new(null);
        private readonly List<IError> _pastErrors = new();

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
        public IEnumerable<IError> PastErrors => _pastErrors;

        /// <inheritdoc />
        public IObservable<IError> Observable => _errors.AsObservable();

        /// <inheritdoc />
        public void Add(Exception error, IMessage originatingMessage = null)
        {
            switch (error)
            {
                case null:
                    return;
                case TaskCanceledException e:
                    return;
                case AggregateException aggregate:
                {
                    foreach (var exception in aggregate.Flatten().InnerExceptions)
                        Log(exception);
                    break;
                }
                
                default:
                    Log(error);
                    break;
            }

            // _log.Error(error.Message, error.StackTrace?.Split(new[] { "in", "at", "(", ")", "[", "]" }, StringSplitOptions.RemoveEmptyEntries)[1] + ' ');
            _pastErrors.Add(new Error(error) { OriginatingMessage = originatingMessage });
            _errors.OnNext(new Error(error) { OriginatingMessage = originatingMessage });
        }

        private void Log(Exception error)
        {
            _log.Error(error.Message, error.StackTrace); 
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
                Timestamp = SystemClock.Instance.GetCurrentInstant();
            }

            /// <inheritdoc />
            public string ErrorType { get; set; }

            /// <inheritdoc />
            public string Message { get; set; }

            /// <inheritdoc />
            public Instant Timestamp { get; set; }

            /// <inheritdoc />
            [JsonIgnore]
            public IMessage OriginatingMessage { get; set; }
        }
    }
}