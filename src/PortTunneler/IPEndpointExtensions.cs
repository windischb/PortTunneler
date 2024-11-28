using System.Net;

namespace PortTunneler;

public static class IpEndpointExtensions
{
    public static IPEndPoint? ToIpEndpoint(this string? value)
    {
        if (value is null)
        {
            return null;
        }
        
        if (!value.Contains(':')) //no port
        {
            return new IPEndPoint(IPAddress.Parse(value), 0);
        }

        if (value.StartsWith(':')) // only port
        {
            return new IPEndPoint(IPAddress.Loopback, int.Parse(value[1..]));
        }
        
        var parts = value.Split(':');
        var address = IPAddress.Parse(parts[0]);
        var port = int.Parse(parts[1]);
        return new IPEndPoint(address, port);
    }
}