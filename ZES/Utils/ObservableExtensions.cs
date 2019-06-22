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

            var obs = Observable.Create(async (IObserver<T> o) =>
            {
                var r = await action();
                if (predicate(r))
                {
                    o.OnNext(r);
                    o.OnCompleted(); 
                }
                else
                {
                    o.OnError(new ArgumentNullException());
                }
            });

            obs = obs.RetryWithDelay(delay);
            if (timeout != TimeSpan.FromMilliseconds(-1))
                obs = obs.Timeout(timeout, Observable.Return(action().Result));

            return await obs;
        }
        
        private static IObservable<T> RetryWithDelay<T>(this IObservable<T> source, TimeSpan timeSpan = default(TimeSpan))
        {
            if (timeSpan == default(TimeSpan))
                timeSpan = Delay;
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (timeSpan < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeSpan));
            if (timeSpan == TimeSpan.Zero)
                return source.RetryWhen(e => Observable.Return(0));

            return source.Catch(Observable.Timer(timeSpan)
                .SelectMany(_ => source)
                .Retry());
        }
    }
}