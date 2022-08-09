using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace Silk
{

    /// <summary>
    /// A base class for tasks which are implemented as a state machine.
    /// Automatically handles keeping track of the next state.
    /// Supports serialization.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public abstract class StateMachineTask<T> : IFiberTask, ISerializationCallbackReceiver where T : StateMachineTask<T>
    {
        /// <summary>
        /// This is used during deserialization to create a delegate to the generic version of <see cref="CreateAwaitingRunStateExecutor{V}"/>.
        /// </summary>
        private static readonly MethodInfo genericCreateRunStateFuncMethodInfo;

        static StateMachineTask()
        {
            // Unfortunately there's no super clean way of getting the MethodInfo of a generic method without doing this.
            genericCreateRunStateFuncMethodInfo = typeof(StateMachineTask<T>)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(x => x.Name == nameof(CreateAwaitingRunStateExecutor) && x.IsGenericMethodDefinition);
        }

        /// <summary>
        /// A function that represents one state of the state machine. Takes no parameters.
        /// </summary>
        /// <returns></returns>
        public delegate TaskStatus RunStateFunc();

        /// <summary>
        /// A function that represents one state of the state machine. Receives the value of an awaited future as its parameter.
        /// </summary>
        /// <typeparam name="V">The type of the value of the future which was being awaited.</typeparam>
        /// <param name="value">The value of the future which was being awaited.</param>
        /// <returns></returns>
        public delegate TaskStatus RunStateFunc<V>(V value);

        [field: SerializeReference]
        public Fiber Fiber { get; set; }

        [field: SerializeReference]
        public FutureTaskResult TaskResultFuture { get; private set; } = new FutureTaskResult();

        /// <summary>
        /// The future which is currently being awaited on by this task.
        /// </summary>
        [SerializeReference]
        private IFuture awaiting;

        /// <summary>
        /// The name of the current nextRunState function.
        /// Used only for serialization purposes.
        /// </summary>
        [SerializeField]
        private string nextRunStateName;

        /// <summary>
        /// The next state function which will be executed by Continue.
        /// If null, assumed to be Run.
        /// </summary>
        private RunStateFunc nextRunState;

        /// <summary>
        /// Executes the next step of the state machine.
        /// </summary>
        /// <returns></returns>
        public TaskStatus Continue()
        {
            if (nextRunState == null)
            {
                nextRunState = Run;
            }

            try
            {
                var result = nextRunState();

                if (result.Kind == TaskStatus.StatusKind.Complete)
                {
                    TaskResultFuture.Value = result.TaskResult;
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                TaskResultFuture.Value = TaskResult.Failure;
                return TaskStatus.Complete(TaskResult.Failure);
            }
        }

        /// <summary>
        /// The first state of the state machine.
        /// Required to be implemented by derived classes.
        /// </summary>
        /// <returns></returns>
        protected abstract TaskStatus Run();

        /// <summary>
        /// Complete with Success.
        /// </summary>
        /// <returns></returns>
        protected TaskStatus Success() => TaskStatus.Complete(TaskResult.Success);

        /// <summary>
        /// Complete with Failure.
        /// </summary>
        /// <returns></returns>
        protected TaskStatus Fail() => TaskStatus.Complete(TaskResult.Failure);

        /// <summary>
        /// Complete with the given taskResult as the result.
        /// </summary>
        /// <returns></returns>
        protected TaskStatus Forward(TaskResult taskResult) => TaskStatus.Complete(taskResult);

        /// <summary>
        /// Yields the fiber, but awaits nothing, and continues to the given state function.
        /// </summary>
        /// <param name="next"></param>
        /// <returns></returns>
        protected TaskStatus Goto(RunStateFunc next)
        {
            Debug.Assert(next.Target == this);
            awaiting = null;
            nextRunState = next;
            nextRunStateName = next.Method.Name;
            return TaskStatus.Waiting(null);
        }

        /// <summary>
        /// Yields the fiber while awaiting the value of future.
        /// The given state function will be executed without a parameter.
        /// </summary>
        /// <param name="future"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        protected TaskStatus WaitFor(IFuture future, RunStateFunc next)
        {
            Debug.Assert(next.Target == this);
            awaiting = future;
            nextRunState = CreateAwaitingRunStateExecutor(next);
            nextRunStateName = next.Method.Name;
            return TaskStatus.Waiting(future);
        }

        /// <summary>
        /// Yields the fiber while awaiting the value of future.
        /// The given state function will be executed with the future's fulfilled value as the parameter.
        /// </summary>
        /// <param name="future"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        protected TaskStatus WaitFor<V>(Future<V> future, RunStateFunc<V> next)
        {
            Debug.Assert(next.Target == this);
            awaiting = future;
            nextRunState = CreateAwaitingRunStateExecutor(next);
            nextRunStateName = next.Method.Name;
            return TaskStatus.Waiting(future);
        }

        /// <summary>
        /// Yields the fiber while waiting for the given other task to complete.
        /// The given state function will be executed without a parameter.
        /// </summary>
        /// <param name="future"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        protected TaskStatus WaitForTask(IFiberTask task, RunStateFunc next)
        {
            Debug.Assert(next.Target == this);
            var future = Fiber.RunTask(task);
            awaiting = future;
            nextRunState = CreateAwaitingRunStateExecutor(next);
            nextRunStateName = next.Method.Name;
            return TaskStatus.Waiting(future);
        }

        /// <summary>
        /// Yields the fiber while waiting for the given other task to complete.
        /// The given state function will be executed with the other task's TaskResult as the parameter.
        /// </summary>
        /// <param name="future"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        protected TaskStatus WaitForTask(IFiberTask effect, RunStateFunc<TaskResult> next)
        {
            Debug.Assert(next.Target == this);
            var future = Fiber.RunTask(effect);
            awaiting = future;
            nextRunState = CreateAwaitingRunStateExecutor(next);
            nextRunStateName = next.Method.Name;
            return TaskStatus.Waiting(future);
        }

        /// <summary>
        /// Yields the fiber while awaiting multiple futures.
        /// Only completes when all the specified futures are fulfilled.
        /// The given state function will be executed without a parameter.
        /// </summary>
        /// <param name="next"></param>
        /// <param name="tasks"></param>
        /// <returns></returns>
        protected TaskStatus WaitForMany(RunStateFunc next, params IFiberTask[] tasks)
        {
            var manyFutures = new FutureTaskResult[tasks.Length];
            for (var i = 0; i < tasks.Length; ++i)
            {
                manyFutures[i] = Fiber.RunTask(tasks[i]);
            }
            var future = Fiber.RunTask(new WaitOnManyTask(manyFutures));
            awaiting = future;
            nextRunState = CreateAwaitingRunStateExecutor(next);
            nextRunStateName = next.Method.Name;
            return TaskStatus.Waiting(future);

        }

        /// <summary>
        /// Yields the fiber while waiting for the given other task to complete.
        /// When resumed, this task will immediately be completed with the same TaskResult as the other task.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        protected TaskStatus BecomeTask(IFiberTask task)
        {
            return WaitForTask(task, ((T)this).Forward);
        }

        /// <summary>
        /// Runs the given task on the same fiber as this task.
        /// </summary>
        /// <param name="task"></param>
        /// <returns>A future value for the given task's TaskResult.</returns>
        protected FutureTaskResult RunTask(IFiberTask task)
        {
            return Fiber.RunTask(task);
        }

        /// <summary>
        /// Creates a thunk which executes the given state function with the awaited future's value as its parameter.
        /// Also clears <see cref="awaiting"/>.
        /// </summary>
        /// <typeparam name="V">The type of the parameter of the state function, and also the type of the awaited future's value.</typeparam>
        /// <param name="next"></param>
        /// <returns></returns>
        private RunStateFunc CreateAwaitingRunStateExecutor<V>(RunStateFunc<V> next) => () =>
        {
            Debug.Assert(awaiting.Fulfilled);
            var result = ((Future<V>)awaiting).Value;
            awaiting = null;
            return next(result);
        };

        /// <summary>
        /// Creates a thunk which executes the given state function without a parameter.
        /// Also clears <see cref="awaiting"/>.
        /// </summary>
        /// <param name="next"></param>
        /// <returns></returns>
        private RunStateFunc CreateAwaitingRunStateExecutor(RunStateFunc next) => () =>
        {
            Debug.Assert(awaiting.Fulfilled);
            awaiting = null;
            return next();
        };

        /// <summary>
        /// Does nothing.
        /// </summary>
        public void OnBeforeSerialize()
        {
            // I could be paranoid and set nextRunStateName here, but that should always be properly set in the first place.
        }

        /// <summary>
        /// Reconstructs the <see cref="nextRunState"/> thunk based on the deserialized <see cref="nextRunStateName"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if bogus values were loaded in deserializaion.</exception>
        public void OnAfterDeserialize()
        {
            // if nextRunStateName is null, then there's no thunk to be created

            if (String.IsNullOrEmpty(nextRunStateName))
            {
                return;
            }

            // get the MethodInfo of the state function

            var nextRunStateMethodInfo = this.GetType().GetMethod(nextRunStateName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (nextRunStateMethodInfo == null)
            {
                Debug.LogError($"Could not find run state method: {nextRunStateName}");
                return;
            }

            var parameters = nextRunStateMethodInfo.GetParameters();

            // when not awaiting anything

            if (awaiting == null)
            {
                if (parameters.Length != 0)
                {
                    throw new NotSupportedException($"State method signature must have 0 arguments because it is not awaiting any future value.");
                }

                nextRunState = (RunStateFunc)Delegate.CreateDelegate(typeof(RunStateFunc), this, nextRunStateMethodInfo);

                return;
            }

            // determine parameter type based on the future value's type

            var parameterType = awaiting.ValueType;

            // when there is no parameter, we can quickly create a parameterless thunk

            if (parameters.Length == 0)
            {
                var runStateFunc = (RunStateFunc)Delegate.CreateDelegate(typeof(RunStateFunc), this, nextRunStateMethodInfo);

                nextRunState = CreateAwaitingRunStateExecutor(runStateFunc);

                return;
            }

            // some basic error checking before proceeding

            if (parameters.Length > 1)
            {
                throw new NotSupportedException($"State method signature must have exactly 0 or 1 parameters.");
            }

            if (parameters[0].ParameterType != parameterType)
            {
                throw new NotSupportedException($"State method must accept same type as the awaiting future result type.");
            }

            // when we need the future value, we have to instantiate the generic method

            var delegateType = typeof(RunStateFunc<>).MakeGenericType(typeof(T), parameterType);

            var nextRunStateDelegate = Delegate.CreateDelegate(delegateType, this, nextRunStateMethodInfo);

            var genericCreateRunStateExecutor = genericCreateRunStateFuncMethodInfo.MakeGenericMethod(parameterType);

            nextRunState = (RunStateFunc)genericCreateRunStateExecutor.Invoke(this, new[] { nextRunStateDelegate });
        }
    }
}
