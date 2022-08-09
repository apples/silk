using System;
using UnityEngine;

namespace Silk
{
    [Serializable]
    public class WaitOnManyTask : IFiberTask
    {
        [SerializeReference]
        private IFuture[] futures;

        [SerializeField]
        private int index;

        public WaitOnManyTask(IFuture[] futures)
        {
            this.futures = futures;
            this.index = 0;
        }

        public Fiber Fiber { get; set; }

        public FutureTaskResult TaskResultFuture { get; private set; } = new FutureTaskResult();

        public TaskStatus Continue()
        {
            if (index < futures.Length)
            {
                var next = futures[index];
                ++index;
                return TaskStatus.Waiting(next);
            }
            else
            {
                return TaskStatus.Complete(TaskResult.Success);
            }
        }
    }
}
