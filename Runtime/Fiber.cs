using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Silk
{
    /// <summary>
    /// A state machine fiber. Handles execution of tasks.
    /// </summary>
    [Serializable]
    public class Fiber
    {
        /// <summary>
        /// Ordered list of pending tasks. Tasks are executed in the order they were queued.
        /// </summary>
        [field: SerializeField]
        private List<PendingStateMachineTask> pendingTasks = new List<PendingStateMachineTask>();

        /// <summary>
        /// This is called when a task is queued by RunTask.
        /// Gives an opportunity to modify the task before it begins executing.
        /// </summary>
        public event Action<IFiberTask> onTaskQueued;

        /// <summary>
        /// Queues a task to be executed. Will call onTaskQueued.
        /// </summary>
        /// <param name="task"></param>
        /// <returns>A future value for the task's TaskResult.</returns>
        public FutureTaskResult RunTask(IFiberTask task)
        {
            task.Fiber = this;
            var taskResultFuture = new FutureTaskResult();
            pendingTasks.Add(new PendingStateMachineTask(task, taskResultFuture));
            if (onTaskQueued != null) onTaskQueued(task);
            return taskResultFuture;
        }

        /// <summary>
        /// Executes the next step of the first pending task.
        /// </summary>
        public bool ExecuteOne()
        {
            var pendingTaskIndex = pendingTasks.FindIndex(x => x.awaiting == null || x.awaiting.Fulfilled);

            if (pendingTaskIndex < 0)
            {
                return false;
            }

            var pendingTask = pendingTasks[pendingTaskIndex];

            try
            {
                var result = pendingTask.task.Continue();

                switch (result.Kind)
                {
                    case TaskStatus.StatusKind.Complete:
                        pendingTask.taskResultFuture.Value = result.TaskResult;
                        pendingTasks.RemoveAt(pendingTaskIndex);
                        break;
                    case TaskStatus.StatusKind.Waiting:
                        pendingTask.awaiting = result.Future;
                        break;
                    default:
                        Debug.LogError($"Unknown StatusKind: {result.Kind}. Treating task as Complete with Failure.");
                        pendingTask.taskResultFuture.Value = TaskResult.Failure;
                        pendingTasks.RemoveAt(pendingTaskIndex);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception occurred while continuing task: {e}");
                pendingTask.taskResultFuture.Value = TaskResult.Failure;
                pendingTasks.RemoveAt(pendingTaskIndex);
            }

            return true;
        }

        /// <summary>
        /// Stops and removes all pending tasks.
        /// </summary>
        public void StopAllTasks()
        {
            foreach (var task in pendingTasks)
            {
                task.taskResultFuture.Value = TaskResult.Stopped;
            }

            pendingTasks.Clear();
        }

        /// <summary>
        /// State of a pending task. May contain an awaited future, which will block the task until fulfilled. 
        /// </summary>
        [Serializable]
        private class PendingStateMachineTask
        {
            /// <summary>
            /// The pending task.
            /// </summary>
            [SerializeReference]
            public IFiberTask task;

            /// <summary>
            /// A future value for this task's eventual TaskResult. 
            /// </summary>
            [SerializeReference]
            public FutureTaskResult taskResultFuture;

            /// <summary>
            /// The future this task is currently awaiting. May be null if not awaiting anything. 
            /// </summary>
            [SerializeReference]
            public IFuture awaiting;

            public PendingStateMachineTask(IFiberTask task, FutureTaskResult taskResultFuture)
            {
                this.task = task;
                this.taskResultFuture = taskResultFuture;
            }
        }
    }
}
