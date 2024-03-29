﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Provides a task scheduler that ensures a maximum concurrency level while
    /// running on top of the thread pool.
    /// </summary>
    public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        // Indicates whether the current thread is processing work items.
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;

        // The list of tasks to be executed
        private readonly LinkedList<Task> _tasks = new LinkedList<Task>(); // protected by lock(_tasks)

        // The maximum concurrency level allowed by this scheduler.
        private readonly int _maxDegreeOfParallelism;

        // Indicates whether the scheduler is currently processing work items.
        private int _delegatesQueuedOrRunning = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="LimitedConcurrencyLevelTaskScheduler"/> class.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">Desired degree of parallelism</param>
        public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1)
                throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }
        
        /// <inheritdoc />
        public sealed override int MaximumConcurrencyLevel => _maxDegreeOfParallelism;

        /// <inheritdoc/>
        protected sealed override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough
            // delegates currently queued or running to process tasks, schedule another.
            lock (_tasks)
            {
                _tasks.AddLast(task);
                if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    ++_delegatesQueuedOrRunning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        /// <inheritdoc />
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            if (!_currentThreadIsProcessingItems)
                return false;

            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued)
            {
                // Try to run the task.
                if (TryDequeue(task))
                    return TryExecuteTask(task);
                else
                    return false;
            }
            else
            {
                return TryExecuteTask(task);
            }
        }

        /// <inheritdoc />
        protected sealed override bool TryDequeue(Task task)
        {
            lock (_tasks)
                return _tasks.Remove(task);
        }

        /// <inheritdoc />
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            var lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken)
                    return _tasks;
                else
                    throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit(_tasks);
            }
        }
        
        // Inform the ThreadPool that there's work to be executed for this scheduler.
        /// Notifies the thread pool of pending work.
        /// This method enqueues work items to be executed by the thread pool.
        /// It processes all available items in the queue and executes them in parallel.
        /// Note:
        /// This method should be used to enable inlining of tasks into the current thread.
        /// Implementation Details:
        /// This method uses ThreadPool.UnsafeQueueUserWorkItem to enqueue the work items.
        /// It gets the next item from the queue, executes it by calling TryExecuteTask,
        /// and continues to process the next item until there are no more items in the queue.
        /// The method sets a flag (_currentThreadIsProcessingItems) to indicate that the current thread is processing items.
        /// Finally, it resets the flag to indicate that processing is complete.
        /// @param None
        /// @return None
        /// /
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(
                _ =>
                {
                    // Note that the current thread is now processing work items.
                    // This is necessary to enable inlining of tasks into this thread.
                    _currentThreadIsProcessingItems = true;
                    try
                    {
                        // Process all available items in the queue.
                        while (true)
                        {
                            Task item;
                            lock (_tasks)
                            {
                                // When there are no more items to be processed,
                                // note that we're done processing, and get out.
                                if (_tasks.Count == 0)
                                {
                                    --_delegatesQueuedOrRunning;
                                    break;
                                }

                                // Get the next item from the queue
                                item = _tasks.First.Value;
                                _tasks.RemoveFirst();
                            }

                            // Execute the task we pulled out of the queue
                            TryExecuteTask(item);
                        }
                    }
                
                    // We're done processing items on the current thread
                    finally
                    {
                        _currentThreadIsProcessingItems = false;
                    }
                }, null);
        }
    }
}