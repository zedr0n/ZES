using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace ZES.Infrastructure.Utils
{
    /// <summary>
    /// Task extensions
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Timeout a task
        /// </summary>
        /// <param name="task">Task to execute</param>
        /// <typeparam name="T">Task result</typeparam>
        /// <returns>Completed task or timeout </returns>
        public static async Task<T> Timeout<T>(this Task<T> task)
        {
            var obs = Observable.Timer(Configuration.Timeout).Select(l => default(T)).Publish().RefCount();
            var timeoutTask = obs.ToTask();
            var result = await await Task.WhenAny(task, timeoutTask); 
            
            if (timeoutTask.IsCompleted)
                throw new TimeoutException();

            return result;
        }

        /// <summary>
        /// Timeout a task
        /// </summary>
        /// <param name="task">Task to execute</param>
        /// <param name="timeout">Explicit timeout duration</param>
        /// <param name="timeoutAction">Clean up action on timeout</param>
        /// <returns>Completed task or timeout </returns>
        /// <exception cref="TimeoutException">Throws if execution of task takes longer than timeout</exception>
        public static async Task Timeout(this Task task, TimeSpan timeout = default, Action timeoutAction = null)
        {
            var delay = Task.Delay(Configuration.Timeout);
            await await Task.WhenAny(task, delay);
            if (delay.IsCompleted)
            {
                timeoutAction?.Invoke();
                throw new TimeoutException();
            }
        }

        /// <summary>
        /// Timeout a task
        /// </summary>
        /// <param name="task">Task to execute</param>
        /// <param name="token">Complete the task on cancellation token</param>
        /// <returns>Completed task or timeout </returns>
        /// <exception cref="TimeoutException">Throws if execution of task takes longer than timeout</exception>
        public static async Task Timeout(this Task task, CancellationToken token)
        {
            var delay = Task.Delay(Configuration.Timeout, token);
            await Task.WhenAny(task, delay);
            if (delay.IsCompleted && !token.IsCancellationRequested)
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