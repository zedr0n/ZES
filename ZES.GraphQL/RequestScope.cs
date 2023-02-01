using System;
using System.Linq;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Language;
using ZES.Interfaces;
using ZES.Interfaces.Clocks;

namespace ZES.GraphQL
{
    /// <summary>
    /// GraphQL request scope
    /// </summary>
    public class RequestScope : IDisposable
    {
        private readonly IRecordLog _recordLog;
        private readonly IRequestContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestScope"/> class.
        /// </summary>
        /// <param name="recordLog">Record log</param>
        /// <param name="context">Request context</param>
        public RequestScope(IRecordLog recordLog, IRequestContext context)
        {
            _recordLog = recordLog;
            _context = context;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            var query = _context.Request.Query.ToString();
            var request = _context.Document.Definitions.OfType<OperationDefinitionNode>()
                .SingleOrDefault(o => o.Name?.Value == _context.Request.OperationName);
            if (request == default)
                return;
            
            switch (request.Operation)
            {
                case OperationType.Query:
                {
                    if (_context.Request.OperationName == null || !_context.Request.OperationName.Contains("introspection"))
                    {
                        var vars = _context.Request.VariableValues;
                        _recordLog.AddQuery(query, _context.Result.ToJson());
                    }
                    
                    break;
                }
                
                case OperationType.Mutation:
                {
                    var arguments = request.SelectionSet.Selections.OfType<FieldNode>().SelectMany(s => s.Arguments);
                    var timestampStr = (string)arguments.SingleOrDefault(x => x.Name.Value == "timestamp" || x.Name.Value == "date")?.Value?.Value;
                    var timestamp = Time.FromExtendedIso(timestampStr);
                    _recordLog.AddMutation(query, timestamp);
                    break;
                }
            }
        }
    } 
}
