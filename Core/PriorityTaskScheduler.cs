using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Helix.Core
{
    public class PriorityTaskScheduler : TaskScheduler
    {
        public static readonly PriorityTaskScheduler Highest = new PriorityTaskScheduler(ThreadPriority.Highest);

        readonly int _maximumConcurrencyLevel;
        readonly BlockingCollection<Task> _tasks;

        public override int MaximumConcurrencyLevel => _maximumConcurrencyLevel;

        PriorityTaskScheduler(ThreadPriority priority)
        {
            _tasks = new BlockingCollection<Task>();
            _maximumConcurrencyLevel = Math.Max(1, Environment.ProcessorCount);

            var threads = new Thread[_maximumConcurrencyLevel];
            for (var threadId = 0; threadId < threads.Length; threadId++)
            {
                threads[threadId] = new Thread(ExecuteTasks)
                {
                    Name = string.Format($"{nameof(PriorityTaskScheduler)}: {threadId}"),
                    Priority = priority,
                    IsBackground = true
                };
                threads[threadId].Start();
            }

            void ExecuteTasks()
            {
                foreach (var task in _tasks.GetConsumingEnumerable()) TryExecuteTask(task);
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks() { return _tasks; }

        protected override void QueueTask(Task task) { _tasks.Add(task); }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) { return false; }
    }
}