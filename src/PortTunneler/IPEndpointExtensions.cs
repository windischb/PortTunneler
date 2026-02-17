using System.Net;

namespace PortTunneler;

public static class IpEndpointExtensions
{
    public static IPEndPoint? ToIpEndpoint(this string? value)
    {
        if (value is null)
            return null;

        if (IPEndPoint.TryParse(value, out var endpoint))
            return endpoint;

        return ParseHostPort(value);
    }

    public static IPEndPoint ParseEndpointOrThrow(string value, string fieldName)
    {
        return value.ToIpEndpoint()
               ?? throw new ArgumentException($"'{value}' is not a valid endpoint for {fieldName}. Expected format: host:port");
    }

    private static IPEndPoint? ParseHostPort(string value)
    {
        if (!DnsCache.TryParseHostPort(value, out var host, out var port))
            return null;

        if (IPAddress.TryParse(host, out var address))
            return new IPEndPoint(address, port);

        return null;
    }
}
