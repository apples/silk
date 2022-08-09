
using System;

namespace Silk
{
    /// <summary>
    /// The result of a task's complete execution.
    /// </summary>
    public enum TaskResult
    {
        /// <summary>
        /// The task succeeded in its job.
        /// </summary>
        Success,

        /// <summary>
        /// The task completed, but did not successfully accomplish its job.
        /// </summary>
        Failure,
    }

    [Serializable]
    public class FutureTaskResult : Future<TaskResult> { }
}
