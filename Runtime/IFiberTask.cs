namespace Silk
{
    /// <summary>
    /// Interface which allows a task to be used in a fiber. 
    /// </summary>
    public interface IFiberTask
    {
        /// <summary>
        /// The fiber which is executing this task. 
        /// </summary>
        Fiber Fiber { get; set; }

        /// <summary>
        /// Executes the next step of the task.
        /// </summary>
        /// <returns></returns>
        TaskStatus Continue();
    }
}
