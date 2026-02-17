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
            "Tunnels": {
                "Services": [
                    { "Name": "sql", "ListenPort": 1433 }
                ]
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.NotNull(config);
        Assert.Single(config.Tunnels.Services);
        Assert.IsType<DiscoverTunnelConfig>(config.Tunnels.Services[0]);
        Assert.Equal("sql", config.Tunnels.Services[0].Name);
        Assert.Equal(1433, config.Tunnels.Services[0].ListenPort);
    }

    [Fact]
    public void Deserialize_ExplicitDiscoverMode()
    {
        const string json = """
        {
            "Tunnels": {
                "Services": [
                    { "Name": "sql", "ListenPort": 1433, "Mode": "Discover" }
                ]
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.IsType<DiscoverTunnelConfig>(config!.Tunnels.Services[0]);
    }

    [Fact]
    public void Deserialize_TunnelMode()
    {
        const string json = """
        {
            "Tunnels": {
                "Services": [
                    { "Name": "sql", "ListenPort": 1433, "Mode": "Tunnel", "ServerAddress": "10.0.0.5:51000" }
                ]
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        var tunnel = Assert.IsType<TunnelTunnelConfig>(config!.Tunnels.Services[0]);
        Assert.Equal("10.0.0.5:51000", tunnel.ServerAddress);
    }

    [Fact]
    public void Deserialize_DirectMode()
    {
        const string json = """
        {
            "Tunnels": {
                "Services": [
                    { "Name": "sql", "ListenPort": 1433, "Mode": "Direct", "TargetAddress": "10.0.0.5:1433" }
                ]
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        var tunnel = Assert.IsType<DirectTunnelConfig>(config!.Tunnels.Services[0]);
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
        Assert.True(config.Server.Enabled);
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
            "Tunnels": {
                "Services": [
                    { "Name": "discover-svc", "ListenPort": 1433 },
                    { "Name": "tunnel-svc", "ListenPort": 1434, "Mode": "Tunnel", "ServerAddress": "10.0.0.5:51000" },
                    { "Name": "direct-svc", "ListenPort": 1435, "Mode": "Direct", "TargetAddress": "10.0.0.5:1433" }
                ]
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Equal(3, config!.Tunnels.Services.Count);
        Assert.IsType<DiscoverTunnelConfig>(config.Tunnels.Services[0]);
        Assert.IsType<TunnelTunnelConfig>(config.Tunnels.Services[1]);
        Assert.IsType<DirectTunnelConfig>(config.Tunnels.Services[2]);
    }

    [Fact]
    public void Deserialize_EmptyConfig_HasDefaults()
    {
        const string json = "{}";

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.NotNull(config);
        Assert.Empty(config.Tunnels.Services);
        Assert.True(config.Tunnels.Enabled);
        Assert.Null(config.Server);
    }

    [Fact]
    public void Deserialize_DiscoveryPorts_Array()
    {
        const string json = """
        {
            "Tunnels": {
                "DiscoveryPorts": [7608, 7609],
                "Services": [
                    { "Name": "svc", "ListenPort": 8080 }
                ]
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);
        Assert.Equal([7608, 7609], config!.Tunnels.DiscoveryPorts);
    }

    [Fact]
    public void Deserialize_DiscoveryPorts_DefaultsToSinglePort()
    {
        const string json = """
        {
            "Tunnels": {
                "Services": []
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);
        Assert.Equal([7608], config!.Tunnels.DiscoveryPorts);
    }

    [Fact]
    public void Deserialize_ServiceTag_OverridesWireTag()
    {
        const string json = """
        {
            "Tunnels": {
                "Services": [
                    { "Name": "My SQL Server", "ListenPort": 1433, "ServiceTag": "sql-prod" }
                ]
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Equal("My SQL Server", config!.Tunnels.Services[0].Name);
        Assert.Equal("sql-prod", config.Tunnels.Services[0].ServiceTag);
        Assert.Equal("sql-prod", config.Tunnels.Services[0].WireTag);
    }

    [Fact]
    public void Deserialize_NoServiceTag_WireTagDefaultsToName()
    {
        const string json = """
        {
            "Tunnels": {
                "Services": [
                    { "Name": "sql", "ListenPort": 1433 }
                ]
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Null(config!.Tunnels.Services[0].ServiceTag);
        Assert.Equal("sql", config.Tunnels.Services[0].WireTag);
    }

    [Fact]
    public void Deserialize_TunnelsEnabled_DefaultsToTrue()
    {
        const string json = """
        {
            "Tunnels": {
                "Services": []
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);
        Assert.True(config!.Tunnels.Enabled);
    }

    [Fact]
    public void Deserialize_TunnelsEnabled_False()
    {
        const string json = """
        {
            "Tunnels": {
                "Enabled": false,
                "Services": []
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);
        Assert.False(config!.Tunnels.Enabled);
    }

    [Fact]
    public void Deserialize_ServerEnabled_DefaultsToTrue()
    {
        const string json = """
        {
            "Server": {
                "Services": []
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);
        Assert.True(config!.Server!.Enabled);
    }

    [Fact]
    public void Deserialize_ServerEnabled_False()
    {
        const string json = """
        {
            "Server": {
                "Enabled": false,
                "Services": []
            }
        }
        """;

        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);
        Assert.False(config!.Server!.Enabled);
    }

    [Fact]
    public void RoundTrip_TunnelMode_PreservesMode()
    {
        var original = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new TunnelTunnelConfig { Name = "sql", ListenPort = 1433, ServerAddress = "10.0.0.5:51000" }
                ]
            }
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Single(deserialized!.Tunnels.Services);
        var tunnel = Assert.IsType<TunnelTunnelConfig>(deserialized.Tunnels.Services[0]);
        Assert.Equal("sql", tunnel.Name);
        Assert.Equal(1433, tunnel.ListenPort);
        Assert.Equal("10.0.0.5:51000", tunnel.ServerAddress);
    }

    [Fact]
    public void RoundTrip_DirectMode_PreservesMode()
    {
        var original = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new DirectTunnelConfig { Name = "web", ListenPort = 8080, TargetAddress = "10.0.0.5:80" }
                ]
            }
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Single(deserialized!.Tunnels.Services);
        var tunnel = Assert.IsType<DirectTunnelConfig>(deserialized.Tunnels.Services[0]);
        Assert.Equal("web", tunnel.Name);
        Assert.Equal("10.0.0.5:80", tunnel.TargetAddress);
    }

    [Fact]
    public void RoundTrip_DiscoverMode_PreservesMode()
    {
        var original = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 }
                ]
            }
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Single(deserialized!.Tunnels.Services);
        Assert.IsType<DiscoverTunnelConfig>(deserialized.Tunnels.Services[0]);
    }

    [Fact]
    public void RoundTrip_ServiceTag_Preserved()
    {
        var original = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new DiscoverTunnelConfig { Name = "My SQL", ListenPort = 1433, ServiceTag = "sql-prod" }
                ]
            }
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Equal("My SQL", deserialized!.Tunnels.Services[0].Name);
        Assert.Equal("sql-prod", deserialized.Tunnels.Services[0].ServiceTag);
        Assert.Equal("sql-prod", deserialized.Tunnels.Services[0].WireTag);
    }

    [Fact]
    public void RoundTrip_MultipleTunnelTypes_AllPreserved()
    {
        var original = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new DiscoverTunnelConfig { Name = "discover-svc", ListenPort = 1433 },
                    new TunnelTunnelConfig
                    {
                        Name = "tunnel-svc", ListenPort = 1434, ServerAddress = "10.0.0.5:51000"
                    },
                    new DirectTunnelConfig { Name = "direct-svc", ListenPort = 1435, TargetAddress = "10.0.0.5:1433" }
                ]
            }
        };

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<PortTunnelerConfig>(json, Options);

        Assert.Equal(3, deserialized!.Tunnels.Services.Count);
        Assert.IsType<DiscoverTunnelConfig>(deserialized.Tunnels.Services[0]);
        Assert.IsType<TunnelTunnelConfig>(deserialized.Tunnels.Services[1]);
        Assert.IsType<DirectTunnelConfig>(deserialized.Tunnels.Services[2]);
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
