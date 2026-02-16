using System.Text.Json;
using FluentAssertions;

namespace PortTunneler.Tests.Unit;

public class ConfigDeserializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    [Fact]
    public void Deserialize_MinimalConfig_DefaultsToDiscover()
    {
        const string json = """
        {
            "Tunnels": [
                { "Name": "sql", "ListenPort": 1433 }
            ]
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        config.Should().NotBeNull();
        config!.Tunnels.Should().HaveCount(1);
        config.Tunnels[0].Should().BeOfType<DiscoverTunnelConfig>();
        config.Tunnels[0].Name.Should().Be("sql");
        config.Tunnels[0].ListenPort.Should().Be(1433);
    }

    [Fact]
    public void Deserialize_ExplicitDiscoverMode()
    {
        const string json = """
        {
            "Tunnels": [
                { "Name": "sql", "ListenPort": 1433, "Mode": "Discover", "DiscoveryPort": 9999 }
            ]
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        var tunnel = config!.Tunnels[0] as DiscoverTunnelConfig;
        tunnel.Should().NotBeNull();
        tunnel!.DiscoveryPort.Should().Be(9999);
    }

    [Fact]
    public void Deserialize_TunnelMode()
    {
        const string json = """
        {
            "Tunnels": [
                { "Name": "sql", "ListenPort": 1433, "Mode": "Tunnel", "ServerAddress": "10.0.0.5:51000" }
            ]
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        var tunnel = config!.Tunnels[0] as TunnelTunnelConfig;
        tunnel.Should().NotBeNull();
        tunnel!.ServerAddress.Should().Be("10.0.0.5:51000");
    }

    [Fact]
    public void Deserialize_DirectMode()
    {
        const string json = """
        {
            "Tunnels": [
                { "Name": "sql", "ListenPort": 1433, "Mode": "Direct", "TargetAddress": "10.0.0.5:1433" }
            ]
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        var tunnel = config!.Tunnels[0] as DirectTunnelConfig;
        tunnel.Should().NotBeNull();
        tunnel!.TargetAddress.Should().Be("10.0.0.5:1433");
    }

    [Fact]
    public void Deserialize_ServerConfig_WithDefaults()
    {
        const string json = """
        {
            "Server": {
                "Services": [
                    { "Name": "sql", "TargetAddress": "127.0.0.1:1433" }
                ]
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        config!.Server.Should().NotBeNull();
        config.Server!.ListenPort.Should().Be(51000);
        config.Server.DiscoveryPort.Should().Be(7608);
        config.Server.Services.Should().HaveCount(1);
        config.Server.Services[0].Name.Should().Be("sql");
        config.Server.Services[0].TargetAddress.Should().Be("127.0.0.1:1433");
    }

    [Fact]
    public void Deserialize_ServerConfig_WithCustomPorts()
    {
        const string json = """
        {
            "Server": {
                "ListenPort": 52000,
                "DiscoveryPort": 8608,
                "Services": []
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        config!.Server!.ListenPort.Should().Be(52000);
        config.Server.DiscoveryPort.Should().Be(8608);
    }

    [Fact]
    public void Deserialize_MultipleTunnelTypes()
    {
        const string json = """
        {
            "Tunnels": [
                { "Name": "discover-svc", "ListenPort": 1433 },
                { "Name": "tunnel-svc", "ListenPort": 1434, "Mode": "Tunnel", "ServerAddress": "10.0.0.5:51000" },
                { "Name": "direct-svc", "ListenPort": 1435, "Mode": "Direct", "TargetAddress": "10.0.0.5:1433" }
            ]
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        config!.Tunnels.Should().HaveCount(3);
        config.Tunnels[0].Should().BeOfType<DiscoverTunnelConfig>();
        config.Tunnels[1].Should().BeOfType<TunnelTunnelConfig>();
        config.Tunnels[2].Should().BeOfType<DirectTunnelConfig>();
    }

    [Fact]
    public void Deserialize_EmptyConfig_HasDefaults()
    {
        const string json = "{}";

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        config.Should().NotBeNull();
        config!.Tunnels.Should().BeEmpty();
        config.Server.Should().BeNull();
    }

    [Fact]
    public void Deserialize_DiscoverDefault_DiscoveryPort()
    {
        const string json = """
        {
            "Tunnels": [
                { "Name": "svc", "ListenPort": 8080 }
            ]
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);
        var discover = config!.Tunnels[0] as DiscoverTunnelConfig;
        discover!.DiscoveryPort.Should().Be(7608);
    }
}
