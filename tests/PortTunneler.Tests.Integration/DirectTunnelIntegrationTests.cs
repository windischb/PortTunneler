using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PortTunneler.Connections;

namespace PortTunneler.Tests.Integration;

public class DirectTunnelIntegrationTests : IAsyncLifetime
{
    private CancellationTokenSource _cts = null!;
    private int _echoPort;
    private int _listenPort;
    private System.Net.Sockets.TcpListener _echoServer = null!;

    public async ValueTask InitializeAsync()
    {
        _cts = new CancellationTokenSource();
        _echoPort = TestHelpers.GetAvailablePort();
        _listenPort = TestHelpers.GetAvailablePort();
        _echoServer = await TestHelpers.StartEchoServer(_echoPort, _cts.Token);
    }

    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _echoServer.Stop();
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DirectTunnel_ForwardsDataToEchoServer()
    {
        var config = new DirectTunnelConfig
        {
            Name = "test-echo",
            ListenPort = _listenPort,
            TargetAddress = $"127.0.0.1:{_echoPort}"
        };

        using var connection = new DirectClientConnection(NullLogger<DirectClientConnection>.Instance, new DnsCache(), config);
        connection.StartListening();

        await Task.Delay(100); // Let listener start

        using var client = await TestHelpers.ConnectWithRetryAsync("127.0.0.1", _listenPort);
        await using var stream = client.GetStream();

        var message = "Hello, Echo!"u8.ToArray();
        await stream.WriteAsync(message);

        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal("Hello, Echo!", Encoding.UTF8.GetString(buffer, 0, bytesRead));

        await connection.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DirectTunnel_MultipleClients_AllGetEchoed()
    {
        var config = new DirectTunnelConfig
        {
            Name = "test-echo-multi",
            ListenPort = _listenPort,
            TargetAddress = $"127.0.0.1:{_echoPort}"
        };

        using var connection = new DirectClientConnection(NullLogger<DirectClientConnection>.Instance, new DnsCache(), config);
        connection.StartListening();

        await Task.Delay(100);

        for (var i = 0; i < 3; i++)
        {
            using var client = await TestHelpers.ConnectWithRetryAsync("127.0.0.1", _listenPort);
            await using var stream = client.GetStream();

            var message = Encoding.UTF8.GetBytes($"Message {i}");
            await stream.WriteAsync(message);

            var buffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(buffer);

            Assert.Equal($"Message {i}", Encoding.UTF8.GetString(buffer, 0, bytesRead));
        }

        await connection.StopAsync(CancellationToken.None);
    }
}
