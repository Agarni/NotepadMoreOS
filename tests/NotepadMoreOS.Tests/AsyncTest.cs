using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace NotepadMoreOS.Tests;

/// <summary>
/// Runs an async test body on a single dedicated thread with a pumped
/// SynchronizationContext. AvaloniaEdit's TextDocument has thread affinity, and
/// xUnit's default context can resume continuations on different pool threads.
/// </summary>
internal static class AsyncTest
{
    public static void Run(Func<Task> test)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            var context = new PumpingSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);

            try
            {
                var task = test();
                while (!task.IsCompleted)
                {
                    context.RunPending();
                    if (!task.IsCompleted)
                    {
                        Thread.Sleep(1);
                    }
                }

                task.GetAwaiter().GetResult();
                context.RunPending();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        })
        {
            IsBackground = true,
            Name = "NotepadMoreOS async test"
        };

        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60)))
        {
            throw new TimeoutException("The async test did not complete within 60 seconds.");
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class PumpingSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        public override void Send(SendOrPostCallback d, object? state) => d(state);

        public void RunPending()
        {
            while (_queue.TryDequeue(out var item))
            {
                item.Callback(item.State);
            }
        }
    }
}
