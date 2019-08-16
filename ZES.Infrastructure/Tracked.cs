using System.Threading.Tasks;

namespace ZES.Infrastructure
{
    
    public class Tracked<T, TResult> 
    {
        public Tracked(T value)
        {
            Tsc = new TaskCompletionSource<TResult>();
            Value = value;
        }
        
        protected readonly TaskCompletionSource<TResult> Tsc;
        public T Value { get; }
        public Task<TResult> Task => Tsc.Task;
        
        public void SetResult(TResult result) { Tsc.SetResult(result); }
    }

    public class Tracked<T> : Tracked<T, bool>
    {
        public Tracked(T value)
            : base(value)
        {
        }

        public void Complete() => Tsc.SetResult(true);
    }
}