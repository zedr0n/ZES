using System;
using System.Threading.Tasks;
using Xunit;
using ZES.Infrastructure;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Utils;

namespace ZES.Tests
{
    public static class TestExtensions
    {
        /// <summary>Verifies that an expression is true.</summary>
        /// <param name="bus">Bus</param>
        /// <param name="query">CQRS query</param>
        /// <param name="predicate">The condition to be inspected</param>
        /// <param name="timeout">Query timeout</param>
        /// <typeparam name="TResult">Result output type</typeparam>
        /// <exception cref="T:Xunit.Sdk.TrueException">Thrown when the condition is false</exception>
        /// <returns>see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task IsTrue<TResult>(this IBus bus, IQuery<TResult> query, Func<TResult, bool> predicate, TimeSpan timeout = default(TimeSpan))
        {
            var r = await bus.QueryUntil(query, predicate, timeout);
            Assert.True(predicate(r));
        }
    }
}