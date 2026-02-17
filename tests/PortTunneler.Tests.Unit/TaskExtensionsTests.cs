namespace PortTunneler.Tests.Unit;

public class TaskExtensionsTests
{
    [Fact]
    public async Task TimeoutAfter_CompletesInTime_ReturnsResult()
    {
        var task = Task.FromResult(42);

        var result = await task.TimeoutAfter(TimeSpan.FromSeconds(5));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task TimeoutAfter_TimesOut_ThrowsTimeoutException()
    {
        var task = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ => 42);

        await Assert.ThrowsAsync<TimeoutException>(() => task.TimeoutAfter(TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task WithTimeout_CompletesInTime_ReturnsIsCompletedTrue()
    {
        var task = Task.FromResult(42);

        var (isCompleted, result) = await task.WithTimeout(TimeSpan.FromSeconds(5));

        Assert.True(isCompleted);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task WithTimeout_TimesOut_ReturnsIsCompletedFalse()
    {
        var task = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ => 42);

        var (isCompleted, result) = await task.WithTimeout(TimeSpan.FromMilliseconds(50));

        Assert.False(isCompleted);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task WithTimeout_Cancellation_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var task = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ => 42);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WithTimeout(TimeSpan.FromSeconds(5), cts.Token));
    }
}
