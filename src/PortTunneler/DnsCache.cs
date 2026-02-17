using System.Collections.Concurrent;
using System.Net;

namespace PortTunneler;

public sealed class DnsCache(TimeSpan? ttl = null)
{
    private readonly TimeSpan _ttl = ttl ?? TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<string, (IPEndPoint Endpoint, DateTime Expiry)> _cache = new();

    public async Task<IPEndPoint> ResolveAsync(string hostPort, CancellationToken ct)
    {
        if (!TryParseHostPort(hostPort, out var host, out var port))
            throw new ArgumentException($"'{hostPort}' is not a valid host:port endpoint.");

        if (IPAddress.TryParse(host, out var ipAddress))
            return new IPEndPoint(ipAddress, port);

        var now = DateTime.UtcNow;
        if (_cache.TryGetValue(hostPort, out var cached) && cached.Expiry > now)
            return cached.Endpoint;

        var addresses = await Dns.GetHostAddressesAsync(host, ct);
        if (addresses.Length == 0)
            throw new ArgumentException($"DNS resolution for '{host}' returned no addresses.");

        var endpoint = new IPEndPoint(addresses[0], port);
        _cache[hostPort] = (endpoint, now + _ttl);
        return endpoint;
    }

    public static bool TryParseHostPort(string value, out string host, out int port)
    {
        host = "";
        port = 0;

        // Handle bracketed IPv6: [::1]:1234
        if (value.StartsWith('['))
        {
            var closeBracket = value.IndexOf(']');
            if (closeBracket < 0)
                return false;

            host = value[1..closeBracket];

            // Expect "]:port" after the bracket
            if (closeBracket + 1 >= value.Length || value[closeBracket + 1] != ':')
                return false;

            var portStr = value[(closeBracket + 2)..];
            return int.TryParse(portStr, out port) && port is >= 1 and <= 65535;
        }

        var colonIndex = value.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex == value.Length - 1)
            return false;

        host = value[..colonIndex];
        var portString = value[(colonIndex + 1)..];

        return int.TryParse(portString, out port) && port is >= 1 and <= 65535;
    }
}
