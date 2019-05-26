using System;
using System.Collections.Generic;

namespace ZES.Interfaces
{
    /// <summary>
    /// Causality graph
    /// </summary>
    public interface ICausalityGraph
    {
        IEnumerable<Guid> GetCauses(Guid messageId);
        IEnumerable<Guid> GetDependents(Guid messageId);
    }
}