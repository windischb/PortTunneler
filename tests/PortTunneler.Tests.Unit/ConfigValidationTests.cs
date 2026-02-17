using Microsoft.Extensions.Logging;
using NSubstitute;

namespace PortTunneler.Tests.Unit;

public class ConfigValidationTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public void Validate_ValidConfig_DoesNotThrow()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 },
                    new DirectTunnelConfig { Name = "web", ListenPort = 8080, TargetAddress = "127.0.0.1:80" }
                ]
            }
        };

        ConfigValidator.Validate(config, _logger);
    }

    [Fact]
    public void Validate_DuplicatePort_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new DiscoverTunnelConfig { Name = "sql1", ListenPort = 1433 },
                    new DiscoverTunnelConfig { Name = "sql2", ListenPort = 1433 }
                ]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("port 1433", ex.Message);
        Assert.Contains("already used", ex.Message);
    }

    [Fact]
    public void Validate_InvalidPort_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services = [new DiscoverTunnelConfig { Name = "sql", ListenPort = 0 }]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("not a valid port", ex.Message);
    }

    [Fact]
    public void Validate_PortTooHigh_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services = [new DiscoverTunnelConfig { Name = "sql", ListenPort = 99999 }]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("not a valid port", ex.Message);
    }

    [Fact]
    public void Validate_EmptyServiceName_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services = [new DiscoverTunnelConfig { Name = "", ListenPort = 1433 }]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("must not be empty", ex.Message);
    }

    [Fact]
    public void Validate_InvalidDirectTargetAddress_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services = [new DirectTunnelConfig { Name = "sql", ListenPort = 1433, TargetAddress = "not-valid" }]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("TargetAddress", ex.Message);
        Assert.Contains("not a valid endpoint", ex.Message);
    }

    [Fact]
    public void Validate_InvalidTunnelServerAddress_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                    [new TunnelTunnelConfig { Name = "sql", ListenPort = 1433, ServerAddress = "badaddr" }]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("ServerAddress", ex.Message);
        Assert.Contains("not a valid endpoint", ex.Message);
    }

    [Fact]
    public void Validate_ServerWithNoServices_LogsWarning()
    {
        var config = new PortTunnelerConfig
        {
            Server = new ServerConfig { Services = [] }
        };

        ConfigValidator.Validate(config, _logger);

        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("no services configured")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Validate_ServerDuplicateServiceName_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Server = new ServerConfig
            {
                Services =
                [
                    new ExposedServiceConfig { Name = "sql", TargetAddress = "127.0.0.1:1433" },
                    new ExposedServiceConfig { Name = "sql", TargetAddress = "127.0.0.1:1434" }
                ]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("duplicate service wire tag", ex.Message);
    }

    [Fact]
    public void Validate_ServerInvalidPort_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Server = new ServerConfig { ListenPort = 0 }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("not a valid port", ex.Message);
    }

    [Fact]
    public void Validate_ServerInvalidServiceTarget_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Server = new ServerConfig
            {
                Services = [new ExposedServiceConfig { Name = "sql", TargetAddress = "invalid" }]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("TargetAddress", ex.Message);
        Assert.Contains("not a valid endpoint", ex.Message);
    }

    [Fact]
    public void Validate_EmptyTunnels_DoesNotThrow()
    {
        var config = new PortTunnelerConfig();

        ConfigValidator.Validate(config, _logger);
    }

    [Fact]
    public void Validate_IPv6BracketedEndpoint_DoesNotThrow()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new DirectTunnelConfig { Name = "sql", ListenPort = 1433, TargetAddress = "[::1]:1433" }
                ]
            }
        };

        ConfigValidator.Validate(config, _logger);
    }

    [Fact]
    public void Validate_HostnameEndpoint_DoesNotThrow()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new TunnelTunnelConfig { Name = "sql", ListenPort = 1433, ServerAddress = "myhost:51000" }
                ]
            }
        };

        ConfigValidator.Validate(config, _logger);
    }

    [Fact]
    public void Validate_HostnameServiceTarget_DoesNotThrow()
    {
        var config = new PortTunnelerConfig
        {
            Server = new ServerConfig
            {
                Services = [new ExposedServiceConfig { Name = "sql", TargetAddress = "dbserver:1433" }]
            }
        };

        ConfigValidator.Validate(config, _logger);
    }

    [Fact]
    public void Validate_DuplicateWireTag_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new DiscoverTunnelConfig { Name = "Display Name 1", ListenPort = 1433, ServiceTag = "sql" },
                    new DiscoverTunnelConfig { Name = "Display Name 2", ListenPort = 1434, ServiceTag = "sql" }
                ]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("duplicate wire tag", ex.Message);
    }

    [Fact]
    public void Validate_EmptyServiceTag_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services = [new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433, ServiceTag = " " }]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("ServiceTag", ex.Message);
        Assert.Contains("must not be empty", ex.Message);
    }

    [Fact]
    public void Validate_DifferentNamesButSameWireTag_ViaDefault_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 },
                    new DiscoverTunnelConfig { Name = "other", ListenPort = 1434, ServiceTag = "sql" }
                ]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("duplicate wire tag", ex.Message);
    }

    [Fact]
    public void Validate_DisabledTunnels_SkipsValidation()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Enabled = false,
                Services = [new DiscoverTunnelConfig { Name = "", ListenPort = 0 }]
            }
        };

        ConfigValidator.Validate(config, _logger);
    }

    [Fact]
    public void Validate_DisabledServer_SkipsValidation()
    {
        var config = new PortTunnelerConfig
        {
            Server = new ServerConfig
            {
                Enabled = false,
                ListenPort = 0
            }
        };

        ConfigValidator.Validate(config, _logger);
    }

    [Fact]
    public void Validate_InvalidDiscoveryPort_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                DiscoveryPorts = [0]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("DiscoveryPorts", ex.Message);
        Assert.Contains("not a valid port", ex.Message);
    }

    [Fact]
    public void Validate_DiscoveryPortTooHigh_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                DiscoveryPorts = [99999]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConfigValidator.Validate(config, _logger));

        Assert.Contains("DiscoveryPorts", ex.Message);
        Assert.Contains("not a valid port", ex.Message);
    }
}
