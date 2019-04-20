using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;

namespace ZES
{
    public static class ObservableExtensions
    {
        private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(25);
        private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(1000);
        
        public static IObservable<T> RetryWithDelay<T>(this IObservable<T> source, TimeSpan timeSpan = default(TimeSpan))
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

        public static async Task<TResult> QueryUntil<TResult>(this IBus bus, IQuery<TResult> query, Func<TResult,bool> predicate = null,TimeSpan timeout = default(TimeSpan), TimeSpan delay = default(TimeSpan))
        {
            //if (timeout == default(TimeSpan))
            //    timeout = TimeSpan.FromSeconds(5);
            return await RetryUntil(async () => await bus.QueryAsync(query), predicate, timeout, delay);
        }

        public static async Task<T> RetryUntil<T>(Func<Task<T>> action,Func<T,bool> predicate = null, TimeSpan timeout = default(TimeSpan), TimeSpan delay = default(TimeSpan))
        {
            if (delay == default(TimeSpan))
                delay = Delay;
            if (timeout == default(TimeSpan))
                timeout = Timeout;
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
                    o.OnError(new ArgumentNullException());
            });

            obs = obs.RetryWithDelay(delay);
            if(timeout != TimeSpan.MaxValue)
                obs = obs.Timeout(timeout);
            return await obs.Catch(Observable.Return(default(T)));
        }
    }
    

}