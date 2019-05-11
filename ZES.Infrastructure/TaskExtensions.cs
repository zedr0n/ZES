using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace ZES.Infrastructure
{
    public static class TaskExtensions
    {
        public static async Task<T> Timeout<T>(this Task<T> task)
        {
            var anyTask = await Task.WhenAny(task, Observable.Timer(Configuration.Timeout).Select(l => default(T)).ToTask());
            return await anyTask;
        }
        
        public static async Task Timeout(this Task task)
        {
            var obs = Observable.Timer(Configuration.Timeout).Publish().RefCount();
            var timeoutTask = obs.ToTask();
            await await Task.WhenAny(task, timeoutTask);
            if (timeoutTask.IsCompleted)
                throw new TimeoutException();
        }
    } 
}