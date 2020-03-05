using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;
using Microsoft.Extensions.DiagnosticAdapter;
using ZES.Interfaces;

#pragma warning disable 1591

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class DiagnosticObserver : IDiagnosticObserver
    {
        private readonly IRecordLog _recordLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticObserver"/> class.
        /// </summary>
        /// <param name="recordLog">GraphQl record log</param>
        public DiagnosticObserver(IRecordLog recordLog)
        {
            _recordLog = recordLog;
        }

        [DiagnosticName("HotChocolate.Execution.Query")]
        public void OnQuery(IQueryContext context)
        {
            // This method is used as marker to enable begin and end events
            // in the case that you want to explicitly track the start and the
            // end of this event.
        }

        [DiagnosticName("HotChocolate.Execution.Query.Start")]
        public void BeginQueryExecute(IQueryContext context)
        {
            var query = context.Request.Query.ToString();
            if (query.Contains("mutation") && !query.Contains("flush") && !query.Contains("Introspection"))
                _recordLog.AddMutation(query);    
        }

        [DiagnosticName("HotChocolate.Execution.Query.Stop")]
        public void EndQueryExecute(IQueryContext context)
        {
            var query = context.Request.Query.ToString();
            if (!query.Contains("mutation"))
            {
                _recordLog.AddQuery(query, context.Result.ToJson());
            }
        }
    }
}