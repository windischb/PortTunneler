using FluentAssertions;
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
            Tunnels =
            [
                new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 },
                new DirectTunnelConfig { Name = "web", ListenPort = 8080, TargetAddress = "127.0.0.1:80" }
            ]
        };

        var act = () => ConfigValidator.Validate(config, _logger);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_DuplicatePort_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels =
            [
                new DiscoverTunnelConfig { Name = "sql1", ListenPort = 1433 },
                new DiscoverTunnelConfig { Name = "sql2", ListenPort = 1433 }
            ]
        };

        var act = () => ConfigValidator.Validate(config, _logger);

        act.Should().Throw<InvalidOperationException>().WithMessage("*port 1433*already used*");
    }

    [Fact]
    public void Validate_InvalidPort_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = [new DiscoverTunnelConfig { Name = "sql", ListenPort = 0 }]
        };

        var act = () => ConfigValidator.Validate(config, _logger);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not a valid port*");
    }

    [Fact]
    public void Validate_PortTooHigh_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = [new DiscoverTunnelConfig { Name = "sql", ListenPort = 99999 }]
        };

        var act = () => ConfigValidator.Validate(config, _logger);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not a valid port*");
    }

    [Fact]
    public void Validate_EmptyServiceName_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = [new DiscoverTunnelConfig { Name = "", ListenPort = 1433 }]
        };

        var act = () => ConfigValidator.Validate(config, _logger);

        act.Should().Throw<InvalidOperationException>().WithMessage("*must not be empty*");
    }

    [Fact]
    public void Validate_InvalidDirectTargetAddress_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = [new DirectTunnelConfig { Name = "sql", ListenPort = 1433, TargetAddress = "not-valid" }]
        };

        var act = () => ConfigValidator.Validate(config, _logger);

        act.Should().Throw<InvalidOperationException>().WithMessage("*TargetAddress*not a valid endpoint*");
    }

    [Fact]
    public void Validate_InvalidTunnelServerAddress_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = [new TunnelTunnelConfig { Name = "sql", ListenPort = 1433, ServerAddress = "badaddr" }]
        };

        var act = () => ConfigValidator.Validate(config, _logger);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ServerAddress*not a valid endpoint*");
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

        var act = () => ConfigValidator.Validate(config, _logger);

        act.Should().Throw<InvalidOperationException>().WithMessage("*duplicate service name*");
    }

    [Fact]
    public void Validate_ServerInvalidPort_Throws()
    {
        var config = new PortTunnelerConfig
        {
            Server = new ServerConfig { ListenPort = 0 }
        };

        var act = () => ConfigValidator.Validate(config, _logger);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not a valid port*");
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

        var act = () => ConfigValidator.Validate(config, _logger);

        act.Should().Throw<InvalidOperationException>().WithMessage("*TargetAddress*not a valid endpoint*");
    }

    [Fact]
    public void Validate_EmptyTunnels_DoesNotThrow()
    {
        var config = new PortTunnelerConfig();

        var act = () => ConfigValidator.Validate(config, _logger);

        act.Should().NotThrow();
    }
}
