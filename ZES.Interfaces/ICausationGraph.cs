using System;
using System.Collections.Generic;

namespace ZES.Interfaces
{
    public interface ICausationGraph
    {
        IEnumerable<Guid> GetCauses(Guid messageId);
        IEnumerable<Guid> GetDependents(Guid messageId);
    }
}