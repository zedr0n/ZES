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
        
        /// <summary>
        /// Timeout a task
        /// </summary>
        /// <param name="task">Task to execute</param>
        /// <returns>Completed task or timeout </returns>
        /// <exception cref="TimeoutException">Throws if execution of task takes longer than timeout</exception>
        public static async Task Timeout(this Task task)
        {
            var obs = Observable.Timer(Configuration.Timeout).Publish().RefCount();
            var timeoutTask = obs.ToTask();
            await await Task.WhenAny(task, timeoutTask);
            if (timeoutTask.IsCompleted)
                throw new TimeoutException();
        }

        /// <summary>
        /// Check if task is completed successfully
        /// </summary>
        /// <param name="task">Task</param>
        /// <returns>True if task is completed successfully</returns>
        public static bool IsSuccessful(this Task task) => task.IsCompleted && !task.IsCanceled && !task.IsFaulted;
    } 
}