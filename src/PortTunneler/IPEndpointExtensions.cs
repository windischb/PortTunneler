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
        var colonIndex = value.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex == value.Length - 1)
            return null;

        var host = value[..colonIndex];
        var portStr = value[(colonIndex + 1)..];

        if (!int.TryParse(portStr, out var port) || port is < 1 or > 65535)
            return null;

        if (IPAddress.TryParse(host, out var address))
            return new IPEndPoint(address, port);

        try
        {
            var addresses = Dns.GetHostAddresses(host);
            if (addresses.Length > 0)
                return new IPEndPoint(addresses[0], port);
        }
        catch
        {
            // Resolution failed
        }

        return null;
    }
}
