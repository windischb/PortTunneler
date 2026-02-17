using Microsoft.Extensions.Logging;
using NSubstitute;

namespace PortTunneler.Tests.Unit;

public class ConfigValidatorTryValidateTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public void TryValidate_ValidConfig_ReturnsTrue()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new DiscoverTunnelConfig { Name = "sql", ListenPort = 1433 }
                ]
            }
        };

        var result = ConfigValidator.TryValidate(config, _logger, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_InvalidConfig_ReturnsFalseWithError()
    {
        var config = new PortTunnelerConfig
        {
            Tunnels = new TunnelsConfig
            {
                Services =
                [
                    new DiscoverTunnelConfig { Name = "", ListenPort = 0 }
                ]
            }
        };

        var result = ConfigValidator.TryValidate(config, _logger, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("validation failed", error);
    }

    [Fact]
    public void TryValidate_DuplicatePort_ReturnsFalseWithError()
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

        var result = ConfigValidator.TryValidate(config, _logger, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("already used", error);
    }

    [Fact]
    public void TryValidate_EmptyConfig_ReturnsTrue()
    {
        var config = new PortTunnelerConfig();

        var result = ConfigValidator.TryValidate(config, _logger, out var error);

        Assert.True(result);
        Assert.Null(error);
    }
}
