#if !NET6_0_OR_GREATER
namespace ControlId.Tests;

/// <summary><c>Task.WaitAsync</c> (.NET 6+) para os testes em net48: falha com <see cref="TimeoutException"/> no prazo.</summary>
internal static class TaskPolyfills
{
    public static async Task WaitAsync(this Task task, TimeSpan timeout)
    {
        if (await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false) != task)
        {
            throw new TimeoutException();
        }

        await task.ConfigureAwait(false);
    }

    public static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout)
    {
        await ((Task)task).WaitAsync(timeout).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }
}
#endif
