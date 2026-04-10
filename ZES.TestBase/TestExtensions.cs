using System;
using System.Threading.Tasks;
using Xunit;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Infrastructure;
using ZES.Utils;

namespace ZES.TestBase
{
    /// <summary>
    /// Provides extension methods for testing CQRS queries on an asynchronous message bus interface.
    /// </summary>
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

        /// <summary>Verifies that an expression is true.</summary>
        /// <param name="bus">Bus</param>
        /// <param name="query">CQRS query</param>
        /// <param name="prop">Property to resolve</param>
        /// <param name="expected">Expected value</param>
        /// <param name="timeout">Query timeout</param>
        /// <typeparam name="TResult">Result output type</typeparam>
        /// <typeparam name="TProperty">Result property to compare</typeparam>
        /// <exception cref="T:Xunit.Sdk.TrueException">Thrown when the condition is false</exception>
        /// <returns>see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task Equal<TResult, TProperty>(this IBus bus, IQuery<TResult> query, Func<TResult, TProperty> prop, TProperty expected, TimeSpan timeout = default(TimeSpan))
        {
            var r = await bus.QueryUntil(query, x => x != null && prop(x) != null && prop(x).Equals(expected), timeout);
            Assert.Equal(expected, prop(r));
        }

        /// <summary>
        /// Validates that a specified property of a CQRS query result matches the expected value with an optional precision comparison for double values.
        /// </summary>
        /// <param name="bus">The asynchronous message bus that executes the query.</param>
        /// <param name="query">The CQRS query to execute.</param>
        /// <param name="prop">A function to extract the property from the query result to be compared.</param>
        /// <param name="expected">The expected value of the specified property.</param>
        /// <param name="timeout">The maximum time allotted for query execution and validation.</param>
        /// <param name="precision">
        /// The number of decimal places to consider when comparing double values.
        /// Negative values indicate that precision is ignored.
        /// </param>
        /// <typeparam name="TResult">The data type of the query result.</typeparam>
        /// <exception cref="T:Xunit.Sdk.EqualException">Thrown if the property does not match the expected value.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task EqualDouble<TResult>(this IBus bus, IQuery<TResult> query, Func<TResult, double> prop, double expected, TimeSpan timeout = default(TimeSpan), int precision = -1)
        {
            var r = await bus.QueryUntil(query, x => x != null && double.Abs(prop(x) - expected) < double.Pow(10, -precision), timeout);
            Assert.Equal(expected, prop(r), precision);
        }
    }
}