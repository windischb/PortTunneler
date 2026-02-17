using System.Net;

namespace PortTunneler.Tests.Unit;

public class DnsCacheTests
{
    [Fact]
    public async Task ResolveAsync_IpLiteral_BypassesCache()
    {
        var cache = new DnsCache();

        var result = await cache.ResolveAsync("127.0.0.1:8080", CancellationToken.None);

        Assert.Equal(IPAddress.Loopback, result.Address);
        Assert.Equal(8080, result.Port);
    }

    [Fact]
    public async Task ResolveAsync_IpLiteral_ReturnsCorrectPort()
    {
        var cache = new DnsCache();

        var result = await cache.ResolveAsync("10.0.0.1:443", CancellationToken.None);

        Assert.Equal(IPAddress.Parse("10.0.0.1"), result.Address);
        Assert.Equal(443, result.Port);
    }

    [Fact]
    public async Task ResolveAsync_Localhost_Resolves()
    {
        var cache = new DnsCache();

        var result = await cache.ResolveAsync("localhost:9090", CancellationToken.None);

        Assert.Equal(9090, result.Port);
        Assert.NotNull(result.Address);
    }

    [Fact]
    public async Task ResolveAsync_CachedResult_ReturnsSameEndpoint()
    {
        var cache = new DnsCache(TimeSpan.FromMinutes(5));

        var first = await cache.ResolveAsync("localhost:8080", CancellationToken.None);
        var second = await cache.ResolveAsync("localhost:8080", CancellationToken.None);

        Assert.Equal(first.Address, second.Address);
        Assert.Equal(first.Port, second.Port);
    }

    [Fact]
    public async Task ResolveAsync_InvalidFormat_ThrowsArgumentException()
    {
        var cache = new DnsCache();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.ResolveAsync("not-valid", CancellationToken.None));
    }

    [Fact]
    public void TryParseHostPort_ValidHostPort_ReturnsTrue()
    {
        var result = DnsCache.TryParseHostPort("myhost:1433", out var host, out var port);

        Assert.True(result);
        Assert.Equal("myhost", host);
        Assert.Equal(1433, port);
    }

    [Fact]
    public void TryParseHostPort_InvalidPort_ReturnsFalse()
    {
        var result = DnsCache.TryParseHostPort("myhost:99999", out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParseHostPort_NoPort_ReturnsFalse()
    {
        var result = DnsCache.TryParseHostPort("myhost", out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParseHostPort_BracketedIPv6_ReturnsTrue()
    {
        var result = DnsCache.TryParseHostPort("[::1]:1234", out var host, out var port);

        Assert.True(result);
        Assert.Equal("::1", host);
        Assert.Equal(1234, port);
    }

    [Fact]
    public async Task ResolveAsync_BracketedIPv6_Resolves()
    {
        var cache = new DnsCache();

        var result = await cache.ResolveAsync("[::1]:8080", CancellationToken.None);

        Assert.Equal(IPAddress.IPv6Loopback, result.Address);
        Assert.Equal(8080, result.Port);
    }

    [Fact]
    public void TryParseHostPort_BracketedIPv6_NoPort_ReturnsFalse()
    {
        var result = DnsCache.TryParseHostPort("[::1]", out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParseHostPort_MalformedBracket_ReturnsFalse()
    {
        var result = DnsCache.TryParseHostPort("[::1:1234", out _, out _);

        Assert.False(result);
    }

    [Fact]
    public async Task ResolveAsync_ConcurrentAccess_DoesNotThrow()
    {
        var cache = new DnsCache();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => cache.ResolveAsync("127.0.0.1:8080", CancellationToken.None));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r =>
        {
            Assert.Equal(IPAddress.Loopback, r.Address);
            Assert.Equal(8080, r.Port);
        });
    }
}
