namespace Silk
{
    /// <summary>
    /// The result of a single step of execution of a task.
    /// If the task has completed, Kind should be Complete, and TaskResult set accordingly.
    /// Otherwise, if the task is awaiting a future value, Kind should be Waiting, and the Future set to the awaited future.
    /// </summary>
    public struct TaskStatus
    {
        /// <summary>
        /// Result kinds. 
        /// </summary>
        public enum StatusKind
        {
            /// <summary>
            /// The task is completed, and will be removed from the tiber. 
            /// </summary>
            Complete,

            /// <summary>
            /// The task is awaiting the value of Future. 
            /// </summary>
            Waiting,
        }

        /// <summary>
        /// The kind of result this is. 
        /// </summary>
        public StatusKind Kind { get; set; }

        /// <summary>
        /// Only set for Complete tasks.
        /// </summary>
        public TaskResult TaskResult { get; set; }

        /// <summary>
        /// Only set for Waiting tasks.
        /// Can be null, in which case the next step of the task will be executed as soon as possible.
        /// </summary>
        public IFuture Future { get; set; }

        /// <summary>
        /// Constructs a Complete result with the given taskResult.
        /// </summary>
        /// <param name="taskResult"></param>
        /// <returns></returns>
        public static TaskStatus Complete(TaskResult taskResult) => new TaskStatus { Kind = StatusKind.Complete, TaskResult = taskResult };

        /// <summary>
        /// Constructs a Waiting result which is awaiting the given future.
        /// </summary>
        /// <param name="future"></param>
        /// <returns></returns>
        public static TaskStatus Waiting(IFuture future) => new TaskStatus { Kind = StatusKind.Waiting, Future = future };
    }
}
