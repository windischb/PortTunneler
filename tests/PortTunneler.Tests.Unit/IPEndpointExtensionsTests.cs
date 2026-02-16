using System.Net;
using FluentAssertions;

namespace PortTunneler.Tests.Unit;

public class IPEndpointExtensionsTests
{
    [Fact]
    public void ToIpEndpoint_Null_ReturnsNull()
    {
        string? value = null;
        value.ToIpEndpoint().Should().BeNull();
    }

    [Fact]
    public void ToIpEndpoint_ValidIpPort_ReturnsEndpoint()
    {
        var result = "127.0.0.1:8080".ToIpEndpoint();

        result.Should().NotBeNull();
        result!.Address.Should().Be(IPAddress.Loopback);
        result.Port.Should().Be(8080);
    }

    [Fact]
    public void ToIpEndpoint_IPEndPointTryParse_Compatible()
    {
        var result = "192.168.1.1:443".ToIpEndpoint();

        result.Should().NotBeNull();
        result!.Port.Should().Be(443);
    }

    [Fact]
    public void ParseEndpointOrThrow_ValidInput_ReturnsEndpoint()
    {
        var result = IpEndpointExtensions.ParseEndpointOrThrow("10.0.0.1:51000", "test");

        result.Address.Should().Be(IPAddress.Parse("10.0.0.1"));
        result.Port.Should().Be(51000);
    }

    [Fact]
    public void ParseEndpointOrThrow_InvalidInput_Throws()
    {
        var act = () => IpEndpointExtensions.ParseEndpointOrThrow("not-valid", "TestField");

        act.Should().Throw<ArgumentException>().WithMessage("*TestField*");
    }

    [Fact]
    public void ToIpEndpoint_JustGarbage_ReturnsNull()
    {
        var result = "garbage".ToIpEndpoint();

        result.Should().BeNull();
    }
}
