using System.Collections.Generic;
using ZES.Interfaces;
using ZES.Interfaces.Net;

namespace ZES.Infrastructure.Net
{
    /// <inheritdoc />
    public class DefaultJsonHandler<T> : IJsonHandler<T>
        where T : IJsonResult
    {
        /// <inheritdoc />
        public IEnumerable<IEvent> Handle(T response) => null;
    }
}