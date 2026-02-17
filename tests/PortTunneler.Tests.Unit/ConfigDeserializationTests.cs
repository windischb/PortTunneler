using System.Text.Json;

namespace PortTunneler.Tests.Unit;

public class ConfigDeserializationTests
{
    private static readonly JsonSerializerOptions Options = PortTunnelerJsonContext.Default.Options;

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

        Assert.NotNull(config);
        Assert.Single(config.Tunnels);
        Assert.IsType<DiscoverTunnelConfig>(config.Tunnels[0]);
        Assert.Equal("sql", config.Tunnels[0].Name);
        Assert.Equal(1433, config.Tunnels[0].ListenPort);
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

        var tunnel = Assert.IsType<DiscoverTunnelConfig>(config!.Tunnels[0]);
        Assert.Equal(9999, tunnel.DiscoveryPort);
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

        var tunnel = Assert.IsType<TunnelTunnelConfig>(config!.Tunnels[0]);
        Assert.Equal("10.0.0.5:51000", tunnel.ServerAddress);
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

        var tunnel = Assert.IsType<DirectTunnelConfig>(config!.Tunnels[0]);
        Assert.Equal("10.0.0.5:1433", tunnel.TargetAddress);
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

        Assert.NotNull(config!.Server);
        Assert.Equal(51000, config.Server!.ListenPort);
        Assert.Equal(7608, config.Server.DiscoveryPort);
        Assert.Single(config.Server.Services);
        Assert.Equal("sql", config.Server.Services[0].Name);
        Assert.Equal("127.0.0.1:1433", config.Server.Services[0].TargetAddress);
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

        Assert.Equal(52000, config!.Server!.ListenPort);
        Assert.Equal(8608, config.Server.DiscoveryPort);
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

        Assert.Equal(3, config!.Tunnels.Count);
        Assert.IsType<DiscoverTunnelConfig>(config.Tunnels[0]);
        Assert.IsType<TunnelTunnelConfig>(config.Tunnels[1]);
        Assert.IsType<DirectTunnelConfig>(config.Tunnels[2]);
    }

    [Fact]
    public void Deserialize_EmptyConfig_HasDefaults()
    {
        const string json = "{}";

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.NotNull(config);
        Assert.Empty(config.Tunnels);
        Assert.Null(config.Server);
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
        var discover = Assert.IsType<DiscoverTunnelConfig>(config!.Tunnels[0]);
        Assert.Equal(7608, discover.DiscoveryPort);
    }

    [Fact]
    public void Deserialize_ServiceTag_OverridesWireTag()
    {
        const string json = """
        {
            "Tunnels": [
                { "Name": "My SQL Server", "ListenPort": 1433, "ServiceTag": "sql-prod" }
            ]
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Equal("My SQL Server", config!.Tunnels[0].Name);
        Assert.Equal("sql-prod", config.Tunnels[0].ServiceTag);
        Assert.Equal("sql-prod", config.Tunnels[0].WireTag);
    }

    [Fact]
    public void Deserialize_NoServiceTag_WireTagDefaultsToName()
    {
        const string json = """
        {
            "Tunnels": [
                { "Name": "sql", "ListenPort": 1433 }
            ]
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Null(config!.Tunnels[0].ServiceTag);
        Assert.Equal("sql", config.Tunnels[0].WireTag);
    }

    [Fact]
    public void RoundTrip_TunnelMode_PreservesMode()
    {
        var original = new PortTunnelerConfig
        {
            Tunnels =
            [
                new TunnelTunnelConfig { Name = "sql", ListenPort = 1433, ServerAddress = "10.0.0.5:51000" }
            ]
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Single(deserialized!.Tunnels);
        var tunnel = Assert.IsType<TunnelTunnelConfig>(deserialized.Tunnels[0]);
        Assert.Equal("sql", tunnel.Name);
        Assert.Equal(1433, tunnel.ListenPort);
        Assert.Equal("10.0.0.5:51000", tunnel.ServerAddress);
    }

    [Fact]
    public void RoundTrip_DirectMode_PreservesMode()
    {
        var original = new PortTunnelerConfig
        {
            Tunnels =
            [
                new DirectTunnelConfig { Name = "web", ListenPort = 8080, TargetAddress = "10.0.0.5:80" }
            ]
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Single(deserialized!.Tunnels);
        var tunnel = Assert.IsType<DirectTunnelConfig>(deserialized.Tunnels[0]);
        Assert.Equal("web", tunnel.Name);
        Assert.Equal("10.0.0.5:80", tunnel.TargetAddress);
    }

    [Fact]
    public void RoundTrip_DiscoverMode_PreservesMode()
    {
        var original = new PortTunnelerConfig
        {
            Tunnels =
            [
                new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433, DiscoveryPort = 9999 }
            ]
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Single(deserialized!.Tunnels);
        var tunnel = Assert.IsType<DiscoverTunnelConfig>(deserialized.Tunnels[0]);
        Assert.Equal(9999, tunnel.DiscoveryPort);
    }

    [Fact]
    public void RoundTrip_ServiceTag_Preserved()
    {
        var original = new PortTunnelerConfig
        {
            Tunnels =
            [
                new DiscoverTunnelConfig { Name = "My SQL", ListenPort = 1433, ServiceTag = "sql-prod" }
            ]
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Equal("My SQL", deserialized!.Tunnels[0].Name);
        Assert.Equal("sql-prod", deserialized.Tunnels[0].ServiceTag);
        Assert.Equal("sql-prod", deserialized.Tunnels[0].WireTag);
    }

    [Fact]
    public void RoundTrip_MultipleTunnelTypes_AllPreserved()
    {
        var original = new PortTunnelerConfig
        {
            Tunnels =
            [
                new DiscoverTunnelConfig { Name = "discover-svc", ListenPort = 1433 },
                new TunnelTunnelConfig { Name = "tunnel-svc", ListenPort = 1434, ServerAddress = "10.0.0.5:51000" },
                new DirectTunnelConfig { Name = "direct-svc", ListenPort = 1435, TargetAddress = "10.0.0.5:1433" }
            ]
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Equal(3, deserialized!.Tunnels.Count);
        Assert.IsType<DiscoverTunnelConfig>(deserialized.Tunnels[0]);
        Assert.IsType<TunnelTunnelConfig>(deserialized.Tunnels[1]);
        Assert.IsType<DirectTunnelConfig>(deserialized.Tunnels[2]);
    }

    [Fact]
    public void Deserialize_ServerServiceTag()
    {
        const string json = """
        {
            "Server": {
                "Services": [
                    { "Name": "My SQL", "TargetAddress": "127.0.0.1:1433", "ServiceTag": "sql-prod" }
                ]
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Equal("My SQL", config!.Server!.Services[0].Name);
        Assert.Equal("sql-prod", config.Server.Services[0].WireTag);
    }
}
