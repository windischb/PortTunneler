using Microsoft.Extensions.Logging;
using NSubstitute;

namespace PortTunneler.Tests.Unit;

public class TcpForwarderTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public async Task ForwardAsync_CopiesData()
    {
        var sourceData = "Hello, World!"u8.ToArray();
        var input = new MemoryStream(sourceData);
        var output = new MemoryStream();

        await TcpForwarder.ForwardAsync(input, output, _logger, "Test", CancellationToken.None);

        Assert.Equal(sourceData, output.ToArray());
    }

    [Fact]
    public async Task ForwardAsync_EmptyStream_CopiesNothing()
    {
        var input = new MemoryStream();
        var output = new MemoryStream();

        await TcpForwarder.ForwardAsync(input, output, _logger, "Test", CancellationToken.None);

        Assert.Empty(output.ToArray());
    }

    [Fact]
    public async Task ForwardAsync_LargeData_CopiesCorrectly()
    {
        var sourceData = new byte[100_000];
        Random.Shared.NextBytes(sourceData);
        var input = new MemoryStream(sourceData);
        var output = new MemoryStream();

        await TcpForwarder.ForwardAsync(input, output, _logger, "Test", CancellationToken.None);

        Assert.Equal(sourceData, output.ToArray());
    }

    [Fact]
    public async Task ForwardAsync_CancellationRequested_StopsGracefully()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var input = new MemoryStream([1, 2, 3]);
        var output = new MemoryStream();

        // Should not throw — cancellation is handled gracefully
        await TcpForwarder.ForwardAsync(input, output, _logger, "Test", cts.Token);
    }
}
