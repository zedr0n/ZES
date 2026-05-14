using System.Collections.Generic;
using ZES.Interfaces.Clocks;

namespace ZES.Interfaces.Domain;

/// <summary>
/// Represents a query result that can carry results for additional historical timestamps.
/// </summary>
/// <typeparam name="TResult">The query result type stored for each historical timestamp.</typeparam>
public interface IHistoricalResults<TResult>
{
    /// <summary>
    /// Gets the query results keyed by the historical timestamp used to build each result.
    /// </summary>
    Dictionary<Time, TResult> HistoricalResults { get; }
}
