using System;
using System.Linq;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Language;
using Microsoft.Extensions.DiagnosticAdapter;
using NodaTime;
using NodaTime.Text;
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

        /// <summary>
        /// Enable the begin/end query handling
        /// </summary>
        /// <param name="context">Query context</param>
        [DiagnosticName("HotChocolate.Execution.Query")]
        public void OnQuery(IQueryContext context)
        {
            // This method is used as marker to enable begin and end events
            // in the case that you want to explicitly track the start and the
            // end of this event.
        }

        /// <summary>
        /// Action to perform before beginning the query 
        /// </summary>
        /// <param name="context">Query context</param>
        [DiagnosticName("HotChocolate.Execution.Query.Start")]
        public void BeginQueryExecute(IQueryContext context)
        {
            var query = context.Request.Query.ToString();
            if (query.Contains("mutation") && !query.Contains("flush") && !query.Contains("Introspection"))
            {
            }
        }

        /// <summary>
        /// Action perform after finishing the query
        /// </summary>
        /// <param name="context">Query context</param>
        [DiagnosticName("HotChocolate.Execution.Query.Stop")]
        public void EndQueryExecute(IQueryContext context)
        {
            var query = context.Request.Query.ToString();
            if (!query.Contains("mutation"))
            {
                var vars = context.Request.VariableValues;
                _recordLog.AddQuery(query, context.Result.ToJson());
            }
            else if (!query.Contains("flush") && !query.Contains("Introspection"))
            {
                var arguments = context.Document.Definitions.OfType<OperationDefinitionNode>()
                    .SelectMany(d => d.SelectionSet.Selections)
                    .OfType<FieldNode>().SelectMany(s => s.Arguments);
                var timestampStr = (string)arguments.SingleOrDefault(x => x.Name.Value == "timestamp")?.Value?.Value;
                var timestamp = default(Instant);
                if (timestampStr != null && !InstantPattern.ExtendedIso.Parse(timestampStr).Success)
                    timestamp = InstantPattern.General.Parse(timestampStr).Value;
                _recordLog.AddMutation(query, timestamp);
            }
        }
    }
}