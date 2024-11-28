namespace PortTunneler;

public static class TaskExtensions
{
    public static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource();
        var delayTask = Task.Delay(timeout, cts.Token);

        if (await Task.WhenAny(task, delayTask) != task)
            throw new TimeoutException();
        
        await cts.CancelAsync();
        return await task;

    }
    
    public static async Task<(bool IsCompleted, T? Result)> WithTimeout<T>(this Task<T> task, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var delayTask = Task.Delay(timeout, cancellationToken);
        var completedTask = await Task.WhenAny(task, delayTask);
        return completedTask == task ? (true, await task) : (false, default(T));
    }

}