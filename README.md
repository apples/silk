# Silk

Silk is a lightweight fiber framework designed primarily for turn-based games.

The core of the framework is very simple, but included is a generic state machine task which can serve as the base for implementing custom state machines.

## Installation

In your Unity project, go to Window -> Package Manager.

Click the + and choose "Add package from git URL...".

Enter this project's URL (`https://github.com/apples/silk.git`) and click Add.

## Creating a Fiber

Fibers are the backbone of this framework. A single Fiber can contain and execute multiple tasks.

You need at least one Fiber to start running tasks.

Suppose you have a `GameManager` script that you want to hold your main Fiber.

Add a field or property to hold the fiber:

```cs
public class GameManager : MonoBehaviour
{
    private Fiber fiber = new Fiber();
}
```

Next, you need to call `ExecuteOne` somewhere to actually execute pending tasks.
In your `Update()` method is a good place:

```cs
public class GameManager : MonoBehaviour
{
    // ...

    Update()
    {
        fiber.ExecuteOne();
    }
}
```

This will only execute one step of a single task per frame. If you need to execute as much as possible, you can use a `while (fiber.ExecuteOne());` loop.

You may also wish to hook into the Fiber's `onTaskQueued` event to be able to modify tasks before they are executed:

```cs
public interface ISomeKindaTask
{
    GameManager GameManager { get; set; }
}

public class GameManager : MonoBehaviour
{
    // ...

    Awake()
    {
        fiber.onTaskQueued += this.fiber_onTaskQueued;
    }

    void fiber_onTaskQueued(IFiberTask task)
    {
        if (task is ISomeKindaTask someKindaTask)
        {
            someKindaTask.GameManager = this;
        }
    }
}
```

## Creating a Task

Tasks are easy to make, but some thought is required in their implementation.

Let's make a task which simply counts to 10, incrementing once each time it is executed:

```cs
public class CountingTask : IFiberTask
{
    public Fiber Fiber { get; set; }

    private int count = 0;

    public TaskStatus Continue()
    {
        ++count;

        Debug.Log($"Counting {count}!");

        if (count < 10)
        {
            return TaskStatus.Waiting(null);
        }
        else
        {
            return TaskStatus.Complete(TaskResult.Success);
        }
    }
}
```

Each time this task is executed, its `Continue()` method will be called.

In the case that it needs to keep counting, it returns `TaskStatus.Waiting`.
This is similar to a `yield` from a coroutine.
It will keep the task queued, and the fiber will execute it again at the next possible opportunity.

When done, it will return `TaskStatus.Complete`.
This is similar to a final `return` from a coroutine.
Completed tasks will be removed from the fiber, and any other tasks which were awaiting this task's completion will be resumed.

## Running a Task

To actually run the task, it needs to be added to the fiber:

```cs
public class GameManager : MonoBehaviour
{
    // ...

    Start()
    {
        fiber.RunTask(new CountingTask());
    }
}
```

It will be executed in the GameManager's `Update()` function, one step every frame.

## Futures

Futures are values which have yet to be determined.

The `Future<T>` class is generic on any value type, but for convenience (and because of some annoyances with Unity), a few starter types are provided:

- `FutureTaskResult`
- `FutureBool`
- `FutureInt`
- `FutureFloat`
- `FutureString`

Tasks can await a future value.

For instance, say we want to modify our `CountingTask` to roll a random number when it's complete.

We can store that random roll in a future:

```cs
public class CountingTask : IFiberTask
{
    // ...

    public FutureFloat FutureRandomRoll = new FutureFloat();

    public TaskStatus Continue()
    {
        ++count;

        // ...

        if (count < 10)
        {
            return TaskStatus.Waiting(null);
        }
        else
        {
            FutureRandomRoll.Value = Random.Range(0f, 100f);

            return TaskStatus.Complete(TaskResult.Success);
        }
    }
}
```

Then, we can make a task which awaits this future value and prints it:

```cs
public class RollPrinterTask : IFiberTask
{
    // ...

    private FutureFloat futureRandomRoll = null;

    public TaskStatus Continue()
    {
        if (futureRandomRoll == null)
        {
            var countingTask = new CountingTask();
            Fiber.RunTask(countingTask);

            futureRandomRoll = countingTask.FutureRandomRoll;

            return TaskStatus.Waiting(futureRandomRoll);
        }
        else
        {
            Debug.Assert(futureRandomRoll.Fulfilled);
            Debug.Log(futureRandomRoll.Value);

            return TaskStatus.Complete(TaskResult.Success);
        }
    }
}

public class GameManager : MonoBehaviour
{
    // ...

    Start()
    {
        fiber.RunTask(new RollPrinterTask());
    }
}
```

This task returns `TaskStatus.Waiting` with the `futureRandomRoll` from the CountingTask as the awaited future.

The RollPrinterTask will not be executed again until the future is fulfilled.

## Using StateMachineTask

The RollPrinterTask above is already getting a little messy.

To make things easier, StateMachineTask exists.
It is expected that almost all of the tasks implemented for a game will be derived from StateMachineTask.

It's probably best to start with an example:

```cs
public class RollPrinterTask : StateMachineTask
{
    protected override TaskStatus Run()
    {
        var countingTask = new CountingTask();
        Fiber.RunTask(countingTask);

        return WaitFor(coutingTask.FutureRandomRoll, PrintResult);
    }

    private TaskStatus PrintResult(float roll)
    {
        Debug.Log(roll);

        return Success();
    }
}
```

The StateMachineTask base class provides many convenience methods, such as `WaitFor()`, `WaitForTask()`, and `Success()`.

The `Wait*` methods in particular are capable of keeping track of an awaited future, and automatically unwrapping it.

In this example, the FutureRandomRoll which is awaited, is unwrapped into the parameter of PrintResult.

## Serialization

All aspects of this framework are serializable.
Primarily, this means that everything is visible within the Unity inspector window during gameplay.

Additionally, theoretically, you could save the state of a Fiber to disk, and reload it later, assuming the tasks are also serializable.

Remember to always use `[SerializeReference]` for Future fields and properties, or they may not be hooked up correctly upon deserialization.
