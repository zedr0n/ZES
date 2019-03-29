using System;
using System.Reactive.Linq;
using System.Threading.Tasks;

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

        public static async Task<T> RetryUntil<T>(Func<Task<T>> action,Func<T,bool> predicate = null, TimeSpan delay = default(TimeSpan), TimeSpan timeout = default(TimeSpan))
        {
            if (delay == default(TimeSpan))
                delay = Delay;
            if (delay == default(TimeSpan))
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

            var dObs = obs.RetryWithDelay(delay);
            return await (timeout == default(TimeSpan) ? dObs : dObs.Timeout(timeout));
        }
    }
    

}