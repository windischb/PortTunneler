using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace PortTunneler.Tests.Integration;

public class MultiplexingTunnelIntegrationTests : IAsyncLifetime
{
    private CancellationTokenSource _cts = null!;
    private int _echoPort;
    private int _serverPort;
    private int _clientPort;
    private System.Net.Sockets.TcpListener _echoServer = null!;
    private IHost? _serverHost;

    public async ValueTask InitializeAsync()
    {
        _cts = new CancellationTokenSource();
        _echoPort = TestHelpers.GetAvailablePort();
        _serverPort = TestHelpers.GetAvailablePort();
        _clientPort = TestHelpers.GetAvailablePort();
        _echoServer = await TestHelpers.StartEchoServer(_echoPort, _cts.Token);

        // Start a PortTunneler server
        var serverConfig = new PortTunnelerConfig
        {
            Server = new ServerConfig
            {
                ListenPort = _serverPort,
                DiscoveryPort = TestHelpers.GetAvailablePort(),
                Services =
                [
                    new ExposedServiceConfig { Name = "echo", TargetAddress = $"127.0.0.1:{_echoPort}" }
                ]
            }
        };

        _serverHost = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(serverConfig);
                services.AddSingleton<DnsCache>();
                services.AddSingleton<DestinationMonitorRegistry>();
                services.AddHostedService<ServerService>();
            })
            .Build();

        await _serverHost.StartAsync(_cts.Token);
        await Task.Delay(200); // Let server start listening
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_serverHost != null)
        {
            await _serverHost.StopAsync(CancellationToken.None);
            _serverHost.Dispose();
        }
        _echoServer.Stop();
        _cts.Dispose();
    }

    [Fact]
    public async Task MultiplexingTunnel_ForwardsDataViaServer()
    {
        var tunnelConfig = new TunnelTunnelConfig
        {
            Name = "echo",
            ListenPort = _clientPort,
            ServerAddress = $"127.0.0.1:{_serverPort}"
        };

        using var connection = new Connections.MultiplexingClientConnection(
            NullLogger<Connections.MultiplexingClientConnection>.Instance, new DnsCache(), tunnelConfig);
        connection.StartListening();

        await Task.Delay(200);

        using var client = await TestHelpers.ConnectWithRetryAsync("127.0.0.1", _clientPort);
        await using var stream = client.GetStream();

        var message = "Hello via multiplexing!"u8.ToArray();
        await stream.WriteAsync(message);

        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal("Hello via multiplexing!", Encoding.UTF8.GetString(buffer, 0, bytesRead));

        await connection.StopAsync(CancellationToken.None);
    }
}
