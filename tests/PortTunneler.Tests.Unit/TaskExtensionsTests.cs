using FluentAssertions;

namespace PortTunneler.Tests.Unit;

public class TaskExtensionsTests
{
    [Fact]
    public async Task TimeoutAfter_CompletesInTime_ReturnsResult()
    {
        var task = Task.FromResult(42);

        var result = await task.TimeoutAfter(TimeSpan.FromSeconds(5));

        result.Should().Be(42);
    }

    [Fact]
    public async Task TimeoutAfter_TimesOut_ThrowsTimeoutException()
    {
        var task = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ => 42);

        var act = () => task.TimeoutAfter(TimeSpan.FromMilliseconds(50));

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task WithTimeout_CompletesInTime_ReturnsIsCompletedTrue()
    {
        var task = Task.FromResult(42);

        var (isCompleted, result) = await task.WithTimeout(TimeSpan.FromSeconds(5));

        isCompleted.Should().BeTrue();
        result.Should().Be(42);
    }

    [Fact]
    public async Task WithTimeout_TimesOut_ReturnsIsCompletedFalse()
    {
        var task = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ => 42);

        var (isCompleted, result) = await task.WithTimeout(TimeSpan.FromMilliseconds(50));

        isCompleted.Should().BeFalse();
        result.Should().Be(0);
    }

    [Fact]
    public async Task WithTimeout_Cancellation_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var task = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ => 42);

        var act = () => task.WithTimeout(TimeSpan.FromSeconds(5), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
