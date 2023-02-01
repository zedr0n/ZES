using System;
using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.DiagnosticAdapter;
using NodaTime;
using NodaTime.Text;
using ZES.Interfaces;

#pragma warning disable 1591

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class DiagnosticListener : ExecutionDiagnosticEventListener
    {
        private readonly IRecordLog _recordLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticListener"/> class.
        /// </summary>
        /// <param name="recordLog">Record log service</param>
        public DiagnosticListener(IRecordLog recordLog)
        {
            _recordLog = recordLog;
        }

        /// <inheritdoc/>
        public override IDisposable ExecuteRequest(IRequestContext context)
        {
            return new RequestScope(_recordLog, context);
        }
    }
}