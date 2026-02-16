using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PortTunneler.Connections;

namespace PortTunneler.Tests.Unit;

public class ClientConnectionFactoryTests
{
    private readonly IClientConnectionFactory _factory;

    public ClientConnectionFactoryTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(new DestinationMonitorRegistry(
            NullLogger<DestinationMonitor>.Instance));
        var sp = services.BuildServiceProvider();
        _factory = new ClientConnectionFactory(sp);
    }

    [Fact]
    public void Create_DirectConfig_CreatesDirectClientConnection()
    {
        var config = new DirectTunnelConfig { Name = "test", ListenPort = 19000, TargetAddress = "127.0.0.1:80" };

        using var connection = _factory.Create(config);

        connection.Should().BeOfType<DirectClientConnection>();
    }

    [Fact]
    public void Create_TunnelConfig_CreatesMultiplexingClientConnection()
    {
        var config = new TunnelTunnelConfig { Name = "test", ListenPort = 19001, ServerAddress = "127.0.0.1:51000" };

        using var connection = _factory.Create(config);

        connection.Should().BeOfType<MultiplexingClientConnection>();
    }

    [Fact]
    public void Create_DiscoverConfig_CreatesDiscoverClientConnection()
    {
        var config = new DiscoverTunnelConfig { Name = "test", ListenPort = 19002 };

        using var connection = _factory.Create(config);

        connection.Should().BeOfType<DiscoverClientConnection>();
    }
}
