using System.Net;

namespace PortTunneler.Tests.Unit;

public class IPEndpointExtensionsTests
{
    [Fact]
    public void ToIpEndpoint_Null_ReturnsNull()
    {
        string? value = null;
        Assert.Null(value.ToIpEndpoint());
    }

    [Fact]
    public void ToIpEndpoint_ValidIpPort_ReturnsEndpoint()
    {
        var result = "127.0.0.1:8080".ToIpEndpoint();

        Assert.NotNull(result);
        Assert.Equal(IPAddress.Loopback, result.Address);
        Assert.Equal(8080, result.Port);
    }

    [Fact]
    public void ToIpEndpoint_IPEndPointTryParse_Compatible()
    {
        var result = "192.168.1.1:443".ToIpEndpoint();

        Assert.NotNull(result);
        Assert.Equal(443, result.Port);
    }

    [Fact]
    public void ParseEndpointOrThrow_ValidInput_ReturnsEndpoint()
    {
        var result = IpEndpointExtensions.ParseEndpointOrThrow("10.0.0.1:51000", "test");

        Assert.Equal(IPAddress.Parse("10.0.0.1"), result.Address);
        Assert.Equal(51000, result.Port);
    }

    [Fact]
    public void ParseEndpointOrThrow_InvalidInput_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            IpEndpointExtensions.ParseEndpointOrThrow("not-valid", "TestField"));

        Assert.Contains("TestField", ex.Message);
    }

    [Fact]
    public void ToIpEndpoint_JustGarbage_ReturnsNull()
    {
        var result = "garbage".ToIpEndpoint();

        Assert.Null(result);
    }
}
