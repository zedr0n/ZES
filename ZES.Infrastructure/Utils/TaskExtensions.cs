using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ZES.Infrastructure.Utils
{
    /// <summary>
    /// Custom awaitable class
    /// </summary>
    public readonly struct CustomTaskAwaitable 
    {
        private readonly CustomTaskAwaiter _awaitable;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomTaskAwaitable"/> struct.
        /// </summary>
        /// <param name="task">Task to wrap</param>
        /// <param name="scheduler">Scheduler to run on</param>
        public CustomTaskAwaitable(Task task, TaskScheduler scheduler)
        {
            _awaitable = new CustomTaskAwaiter(task, scheduler);
        }

        /// <summary />
        /// <returns>Awaiter instance</returns>
        public CustomTaskAwaiter GetAwaiter() { return _awaitable; }

        /// <summary />
        public readonly struct CustomTaskAwaiter : INotifyCompletion 
        {
            private readonly Task _task;
            private readonly TaskScheduler _scheduler;

            /// <summary>
            /// Initializes a new instance of the <see cref="CustomTaskAwaiter"/> struct.
            /// </summary>
            /// <param name="task">Task to wrap</param>
            /// <param name="scheduler">Scheduler to use</param>
            public CustomTaskAwaiter(Task task, TaskScheduler scheduler) 
            {
                this._task = task;
                this._scheduler = scheduler;
            }

            /// <inheritdoc cref="INotifyCompletion" />
            public bool IsCompleted => _task.IsCompleted;

            /// <inheritdoc />
            public void OnCompleted(Action continuation)
            {
                // ContinueWith sets the scheduler to use for the continuation action
                _task.ContinueWith(x => continuation(), _scheduler);
            }

            /// <inheritdoc cref="INotifyCompletion" />
            public void GetResult() { }
        }
    }

    /// <summary>
    /// Task extensions
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// Gets the custom await task using the specified scheduler
        /// </summary>
        /// <param name="task">Original task</param>
        /// <param name="scheduler">Scheduler to use</param>
        /// <returns>Awaitable task using the custom scheduler</returns>
        public static CustomTaskAwaitable ConfigureScheduler(this Task task, TaskScheduler scheduler) 
        {
            return new CustomTaskAwaitable(task, scheduler);
        }
        
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
        /// <returns>Completed task or timeout </returns>
        /// <exception cref="TimeoutException">Throws if execution of task takes longer than timeout</exception>
        public static async Task<bool> Timeout(this Task task, TimeSpan timeout = default)
        {
            if (timeout == default)
                timeout = Configuration.Timeout;
            
            var delay = Task.Delay(timeout);
            await Task.WhenAny(task, delay);
            return !delay.IsCompleted;
        }

        /// <summary>
        /// Timeout a task
        /// </summary>
        /// <param name="task">Task to execute</param>
        /// <param name="token">Complete the task on cancellation token</param>
        /// <returns>Completed task or timeout </returns>
        /// <exception cref="TimeoutException">Throws if execution of task takes longer than timeout</exception>
        public static async Task<bool> Timeout(this Task task, CancellationToken token)
        {
            var delay = Task.Delay(Configuration.Timeout, token);
            await Task.WhenAny(task, delay);
            return !delay.IsCompleted || token.IsCancellationRequested;
        }

        /// <summary>
        /// Check if task is completed successfully
        /// </summary>
        /// <param name="task">Task</param>
        /// <returns>True if task is completed successfully</returns>
        public static bool IsSuccessful(this Task task) => task.IsCompleted && !task.IsCanceled && !task.IsFaulted;
    } 
}