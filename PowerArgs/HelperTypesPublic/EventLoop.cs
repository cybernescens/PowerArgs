using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace PowerArgs;

public class EventLoop : Lifetime
{
    public enum EventLoopExceptionHandling
    {
        Throw,
        Stop,
        Swallow
    }

    private readonly ConcurrentQueue<(SynchronizedEvent Work, string? Name)> pendQueue = new();
    private readonly ConcurrentQueue<(SynchronizedEvent Work, string? Name)> workQueue = new();
    private readonly CancellationTokenSource cts = new();
    
    private Task? runTask;
    private bool asyncMode;
    private bool drained;

    public const double MaxConcurrentCycleEvents = 1d;

    public Event StartOfCycle { get; } = new();
    public Event EndOfCycle { get; } = new();
    public Event LoopStarted { get; } = new();
    public Event LoopStopped { get; } = new();

    public long Posts => SynchronizationContext.Current is EventLoopSyncContext loop ? loop.Posts : 0;
    public long Sends => SynchronizationContext.Current is EventLoopSyncContext loop ? loop.Sends : 0;
    public long AsyncContinuations => Posts + Sends;

    public bool IsRunning => runTask != null;
    public bool IsFinished => runTask is { IsCompleted: true } or { IsFaulted: true } or { IsCanceled: true };

    public ulong Cycle { get; private set; } = ulong.MinValue;
    public bool IsDrainingOrDrained => drained;

    /// <summary>
    ///     Runs the event loop on a new thread
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public virtual Task Start()
    {
        asyncMode = true;
        runTask = Task.Factory.StartNew(RunCommon, cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        return runTask;
    }

    /// <summary>
    ///     Runs the event loop using the current thread
    /// </summary>
    public virtual void Run()
    {
        asyncMode = false;
        runTask = Task.Factory.StartNew(RunCommon, cts.Token, TaskCreationOptions.None, TaskScheduler.Default);
        runTask.Wait(cts.Token);
    }

    private void RunCommon()
    {
        SynchronizationContext.SetSynchronizationContext(new EventLoopSyncContext(this));
        Loop();
        SynchronizationContext.SetSynchronizationContext(null);
    }

    private void Loop()
    {
        try
        {
            LoopStarted.Fire();

            while (true)
            {
                Cycle = Cycle == ulong.MaxValue ? ulong.MinValue : Cycle + 1;

                try
                {
                    StartOfCycle.Fire();
                }
                catch (Exception ex)
                {
                    switch (HandleWorkItemException(IsDrainingOrDrained, ex, out var rethrow))
                    {
                        case EventLoopExceptionHandling.Throw: throw rethrow!;
                        case EventLoopExceptionHandling.Stop:
                            cts.Cancel();
                            return;
                        case EventLoopExceptionHandling.Swallow:
                        default:
                            break;
                    }
                }

                var cycleQueue = new ConcurrentQueue<(SynchronizedEvent Work, string? Name)>();

                while (workQueue.Any())
                {
                    (SynchronizedEvent Work, string? Name) workItem;
                    while (!workQueue.TryDequeue(out workItem)) { }

                    /* these tasks have not been started yet, so just discard */
                    if (cts.Token.IsCancellationRequested)
                        continue;

                    /* do not start yet */
                    cycleQueue.Enqueue(workItem);
                }

                /* cycle through only once per cycle; these should all already be running */
                for (var i = 0; i < pendQueue.Count || (cts.Token.IsCancellationRequested && pendQueue.Any()); i++)
                {
                    (SynchronizedEvent Work, string? Name) pendItem;
                    while (!pendQueue.TryDequeue(out pendItem)) { }

                    /* not sure if we want to dequeue all or just exit */
                    if (pendItem.Work.IsFinished || cts.Token.IsCancellationRequested)
                        continue;

                    if (!pendItem.Work.IsFinished)
                    {
                        pendQueue.Enqueue(pendItem);
                        continue;
                    }

                    if (pendItem.Work.IsFinished && pendItem.Work.IsFailed)
                    {
                        switch (HandleWorkItemException(IsDrainingOrDrained, pendItem.Work.Exception, out var rethrow))
                        {
                            case EventLoopExceptionHandling.Throw: throw rethrow!;
                            case EventLoopExceptionHandling.Stop:
                                cts.Cancel();
                                return;
                            case EventLoopExceptionHandling.Swallow:
                            default:
                                break;
                        }
                    }
                }

                for (; cycleQueue.Any();)
                {
                    (SynchronizedEvent Work, string? Name) cycleItem;
                    while (!cycleQueue.TryDequeue(out cycleItem)) { }

                    /* if a stop has been requested and the task not started yet then don't start it, just exit */
                    if (cycleItem.Work.IsFinished || cts.IsCancellationRequested)
                        continue;

                    //if (cts.IsCancellationRequested)
                    //    cts.Token.ThrowIfCancellationRequested();

                    cycleItem.Work.Run(cts.Token);

                    if (!cycleItem.Work.IsFinished)
                    {
                        pendQueue.Enqueue(cycleItem);
                        continue;
                    }

                    if (cycleItem.Work.IsFinished && cycleItem.Work.IsFailed)
                    {
                        switch (HandleWorkItemException(IsDrainingOrDrained, cycleItem.Work.Exception, out var rethrow))
                        {
                            case EventLoopExceptionHandling.Throw: throw rethrow!;
                            case EventLoopExceptionHandling.Stop:
                                cts.Cancel();
                                return;
                            case EventLoopExceptionHandling.Swallow:
                            default:
                                break;
                        }
                    }
                }

                if (cts.Token.IsCancellationRequested && !pendQueue.Any())
                {
                    Console.WriteLine("Cancelled main loop and all queues are now empty.");
                    cts.Token.ThrowIfCancellationRequested();
                }

                try
                {
                    EndOfCycle.Fire();
                }
                catch (Exception ex)
                {
                    switch (HandleWorkItemException(IsDrainingOrDrained, ex, out var rethrow))
                    {
                        case EventLoopExceptionHandling.Throw: throw rethrow!;
                        case EventLoopExceptionHandling.Stop:
                            cts.Cancel();
                            return;
                        case EventLoopExceptionHandling.Swallow:
                        default:
                            break;
                    }
                }
            }
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine("Main Loop Has been Cancelled");
            Console.WriteLine(e.Message);
        }
        finally
        {
            drained = true;
            LoopStopped.Fire();
        }
    }

    //private void ParallelProcess<T>(ConcurrentQueue<T> queue, Action<T> onDequeue)
    //{
    //    var batchSize = Convert.ToInt32(queue.Count * (1 / MaxConcurrentCycleEvents));
    //    var parts = queue.Count / batchSize;
    //    var actions = Enumerable
    //        .Range(1, parts)
    //        .Select(i => (Action)(() => {
    //            var start = i * batchSize;
    //            var end = Math.Min(start + batchSize, queue.Count);
                
    //        }))
    //        .ToArray();

    //    Parallel.Invoke(actions);
    //}

    public void Stop()
    {
        if (IsRunning && !IsDrainingOrDrained)
            cts.Cancel();
    }

    public void Invoke(Action work) =>
        Invoke(
            () => {
                work();
                return Task.CompletedTask;
            });

    public void InvokeNextCycle(Action work, [CallerMemberName] string? debugName = null)
    {
        if (IsRunning || !IsDrainingOrDrained)
            workQueue.Enqueue((new ActionSynchronizedEvent(work), debugName));
    }

    public void InvokeNextCycle(Func<Task> work, [CallerMemberName] string? debugName = null)
    {
        if (IsRunning || !IsDrainingOrDrained)
            workQueue.Enqueue((new TaskSynchronizedEvent(work), debugName));
    }
    
    public void InvokeNextCycle(SendOrPostCallback callback, object? state)
    {
        if (IsRunning || !IsDrainingOrDrained)
            workQueue.Enqueue((new SendOrPostCallbackSynchronizedEvent(callback, state), null));
    }

    public Task InvokeAsync(Action a)
    {
        return InvokeAsync(
            () => {
                a();
                return Task.CompletedTask;
            });
    }

    public Task InvokeAsync(Func<Task> work)
    {
        var task = work();
        Invoke(() => task);
        return task;
    }

    public void Invoke(Func<Task> work, [CallerMemberName] string? debugName = null)
    {
        if (!IsRunning && IsDrainingOrDrained)
        {
            return;
        }

        var workItem = new TaskSynchronizedEvent(work);

        /* run this version if we are in async mode */

        if (asyncMode)
        {
            workQueue.Enqueue((workItem, debugName));
            return;
        }

        /* run this version if we are in sync mode */

        workItem.Run(cts.Token);

        if (!workItem.IsFinished)
        {
            pendQueue.Enqueue((workItem, debugName));
            return;
        }

        if (workItem.IsFailed)
        {
            switch (HandleWorkItemException(IsDrainingOrDrained, workItem.Exception, out var rethrow))
            {
                case EventLoopExceptionHandling.Throw: throw rethrow!;
                case EventLoopExceptionHandling.Stop:
                    cts.Cancel();
                    return;
                case EventLoopExceptionHandling.Swallow:
                default:
                    return;
            }
        }
    }

    private static EventLoopExceptionHandling HandleWorkItemException(bool draining, Exception? ex, out Exception? rethrow)
    {
        rethrow = null;

        if (ex == null)
            return EventLoopExceptionHandling.Swallow;

        var flattened = ex.Clean();

        if (draining) 
            return EventLoopExceptionHandling.Swallow;

        rethrow = new AggregateException(flattened);
        return EventLoopExceptionHandling.Throw;
    }

    private class TaskSynchronizedEvent : SynchronizedEvent
    {
        private readonly Func<Task> work;

        public TaskSynchronizedEvent(Func<Task> work)
        {
            this.work = work;
        }

        protected override Task CreateTask(CancellationToken ct) =>
            Task.Factory.StartNew(
                work,
                ct,
                TaskCreationOptions.AttachedToParent,
                TaskScheduler.Default);
    }

    private class ActionSynchronizedEvent : SynchronizedEvent
    {
        private readonly Action action;

        public ActionSynchronizedEvent(Action action)
        {
            this.action = action;
        }

        protected override Task CreateTask(CancellationToken ct) =>
            Task.Factory.StartNew(
                action,
                ct,
                TaskCreationOptions.AttachedToParent,
                TaskScheduler.Default);
    }

    private class SendOrPostCallbackSynchronizedEvent : SynchronizedEvent
    {
        private readonly SendOrPostCallback callback;
        private readonly object? state;

        public SendOrPostCallbackSynchronizedEvent(SendOrPostCallback callback, object? state)
        {
            this.callback = callback;
            this.state = state;
        }

        protected override Task CreateTask(CancellationToken ct) => 
            Task.Factory.StartNew(
                o => { callback.Invoke(o); }, 
                state,
                ct,
                TaskCreationOptions.AttachedToParent,
                TaskScheduler.Default);
    }

    private abstract class SynchronizedEvent
    {
        private Task? task;

        public bool IsFinished => task is { IsCompleted: true };
        public bool IsFailed => task is { IsFaulted: true };
        public Exception? Exception => task?.Exception;

        protected abstract Task CreateTask(CancellationToken ct);

        public Task Run(CancellationToken ct = default)
        {
            task = CreateTask(ct);
            return task;
        }

        public void RunSynchronously()
        {
            var t = Run();
            while (!t.IsCompleted) { }
        }
    }

    private class EventLoopSyncContext : SynchronizationContext
    {
        private EventLoop? loop;

        public long Posts;
        public long Sends;

        public EventLoopSyncContext(EventLoop loop)
        {
            this.loop = loop;
            loop.OnDisposed(() => this.loop = null);
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            if (loop is { IsRunning: true, IsDrainingOrDrained: false })
            {
                Posts++;
                loop.InvokeNextCycle(d, state);
            }
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            if (loop is { IsRunning: true, IsDrainingOrDrained: false })
            {
                Sends++;
                loop.InvokeNextCycle(() => d.Invoke(state));
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}