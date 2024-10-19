using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gridsum.DataflowEx;

namespace ZES.Infrastructure.Utils
{
    /// <summary>
    /// Dataflow extension methods
    /// </summary>
    public static class DataflowExtensions
    {
        /// <summary>
        /// Gets the default execution dataflow block options
        /// </summary>
        /// <param name="options">Base options</param>
        /// <param name="isBlockMultiThreaded">Can block run multiple threads</param>
        /// <param name="useScheduler">Use limited concurrency scheduler</param>
        /// <param name="maxMessagesPerTask">Maximum messages per task</param>
        /// <returns>Execution dataflow block options</returns>
        public static ExecutionDataflowBlockOptions ToDataflowBlockOptions(
            this DataflowOptions options,
            bool isBlockMultiThreaded = false,
            bool useScheduler = false,
            int maxMessagesPerTask = -1)
        {
            var executionOptions = options.ToExecutionBlockOption(isBlockMultiThreaded);
            if (Configuration.MaxMessagesPerTask > 0)
                maxMessagesPerTask = Configuration.MaxMessagesPerTask;
            
            if (maxMessagesPerTask > 0)
                executionOptions.MaxMessagesPerTask = maxMessagesPerTask;

            if (useScheduler)
                executionOptions.TaskScheduler = Configuration.UseLimitedScheduler ? Configuration.LimitedTaskScheduler : TaskScheduler.Default;
            return executionOptions;
        }
        
        /// <summary>
        /// Receive asynchronously with the defined predicate
        /// </summary>
        /// <param name="bufferBlock">Combined buffer block</param>
        /// <param name="filter">Filter predicate</param>
        /// <typeparam name="TIn">Item type</typeparam>
        /// <returns>Filtered item if available</returns>
        public static Task<TIn> ReceiveAsync<TIn>(this ISourceBlock<TIn> bufferBlock, Predicate<TIn> filter)
        {
            return bufferBlock.ReceiveAsync(filter, CancellationToken.None);
        }

        /// <summary>
        /// Receive asynchronously with the defined predicate
        /// </summary>
        /// <param name="bufferBlock">Combined buffer block</param>
        /// <param name="filter">Filter predicate</param>
        /// <param name="token">Cancellation token</param>
        /// <typeparam name="TIn">Item type</typeparam>
        /// <returns>Filtered item if available</returns>
        public static Task<TIn> ReceiveAsync<TIn>(this ISourceBlock<TIn> bufferBlock, Predicate<TIn> filter, CancellationToken token)
        {
            var block = new BufferBlock<TIn>();

            bufferBlock.LinkTo(
                block,
                new DataflowLinkOptions { MaxMessages = 1, PropagateCompletion = true },
                filter);

            return block.ReceiveAsync(token);
        }
        
        /// <summary>
        /// Process the specified number of inputs to outputs via the async dataflow 
        /// </summary>
        /// <param name="dataflow">Target dataflow</param>
        /// <param name="input">Input enumerable</param>
        /// <param name="output">Transformed output</param>
        /// <param name="count">Count to process</param>
        /// <typeparam name="TIn">Input type</typeparam>
        /// <typeparam name="TOut">Transformed type</typeparam>
        /// <returns>True if all input has been processed</returns>
        public static async Task<bool> ProcessAsync<TIn, TOut>(this Dataflow<TIn, TOut> dataflow, IEnumerable<TIn> input, List<TOut> output, int count)
        {
            var bufferBlock = new BufferBlock<TOut>();
            dataflow.OutputBlock.LinkTo(bufferBlock, new DataflowLinkOptions { MaxMessages = count } );
            await dataflow.ProcessAsync(input.Take(count), false);
            
            while (output.Count < count)
            {
                await bufferBlock.OutputAvailableAsync(); // .Timeout(Configuration.Timeout);
                if (!bufferBlock.TryReceiveAll(out var newEvents))
                    break;
                output.AddRange(newEvents);
            }

            return output.Count == count;
        }
    }
}