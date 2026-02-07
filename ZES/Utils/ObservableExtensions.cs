using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Infrastructure;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES.Utils
{
    /// <summary>
    /// Multi-threaded awaiter for unit testing
    /// </summary>
    public static class ObservableExtensions
    {
        private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(25);

        /// <summary>
        /// Gets the current value of the observable if available
        /// </summary>
        /// <param name="observable">Rx observable</param>
        /// <typeparam name="T">Observable type</typeparam>
        /// <returns>Current value of the observable</returns>
        public static T Current<T>(this IObservable<T> observable)
        {
            var state = default(T);
            var sub = observable.Subscribe(s => state = s);
            sub.Dispose();
            return state;
        }
        
        /// <summary>
        /// Repeated query until condition is satisfied or timeout is reached 
        /// </summary>
        /// <param name="bus">Message bus</param>
        /// <param name="query">CQRS query</param>
        /// <param name="predicate">Stop condition</param>
        /// <param name="timeout">Execution timeout</param>
        /// <param name="delay">Delay between repeating the bus calls</param>
        /// <typeparam name="TResult">Query result</typeparam>
        /// <returns>Task representing the asynchronous repeated query</returns>
        public static async Task<TResult> QueryUntil<TResult>(this IBus bus, IQuery<TResult> query, Func<TResult, bool> predicate = null, TimeSpan timeout = default(TimeSpan), TimeSpan delay = default(TimeSpan))
        {
            return await RetryUntil(async () => await bus.QueryAsync(query), predicate, timeout, delay);
        }

        /// <summary>
        /// Repeated repository access until the aggregate root is found or timeout is reached 
        /// </summary>
        /// <param name="repository">Aggregate root repository</param>
        /// <param name="id">Aggregate root id</param>
        /// <param name="predicate">Stop condition</param>
        /// <param name="timeout">Execution timeout</param>
        /// <param name="delay">Delay between repeating the bus calls</param>
        /// <typeparam name="T">Aggregate root type</typeparam>
        /// <returns>Task representing the asynchronous repeated find</returns>
        public static async Task<T> FindUntil<T>(this IEsRepository<IAggregate> repository, string id, Func<T, bool> predicate = null, TimeSpan timeout = default(TimeSpan), TimeSpan delay = default(TimeSpan))
            where T : class, IAggregate, new()
        {
            return await RetryUntil(async () => await repository.Find<T>(id));
        }

        private static async Task<T> RetryUntil<T>(Func<Task<T>> action, Func<T, bool> predicate = null, TimeSpan timeout = default(TimeSpan), TimeSpan delay = default(TimeSpan))
        {
            if (delay == default(TimeSpan))
                delay = Delay;
            if (timeout == default(TimeSpan))
                timeout = Configuration.Timeout;
            if (predicate == null)
                predicate = x => !Equals(x, default(T));

            var startTime = DateTime.UtcNow;
            var endTime = timeout == TimeSpan.FromMilliseconds(-1) 
                ? DateTime.MaxValue 
                : startTime.Add(timeout);

            while (true)
            {
                var result = await action();
                if (predicate(result))
                    return result;

                if (DateTime.UtcNow >= endTime)
                    return result; // Timeout - return last result

                await Task.Delay(delay);
            }
        }
    }
}