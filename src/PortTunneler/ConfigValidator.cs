using System.Net;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public static class ConfigValidator
{
    public static void Validate(PortTunnelerConfig config, ILogger logger)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        ValidateTunnels(config.Tunnels, errors, warnings);
        ValidateServer(config.Server, errors, warnings);

        foreach (var warning in warnings)
        {
            logger.LogWarning("{Warning}", warning);
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Configuration validation failed:\n" + string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }

    private static void ValidateTunnels(IReadOnlyList<TunnelConfig> tunnels, List<string> errors, List<string> warnings)
    {
        var usedPorts = new HashSet<int>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < tunnels.Count; i++)
        {
            var tunnel = tunnels[i];
            var prefix = $"Tunnels[{i}]";

            if (string.IsNullOrWhiteSpace(tunnel.Name))
            {
                errors.Add($"{prefix}.Name: must not be empty.");
            }
            else if (!usedNames.Add(tunnel.Name))
            {
                warnings.Add($"{prefix}.Name: duplicate tunnel name '{tunnel.Name}'.");
            }

            if (tunnel.ListenPort is < 1 or > 65535)
            {
                errors.Add($"{prefix}.ListenPort: {tunnel.ListenPort} is not a valid port (1-65535).");
            }
            else if (!usedPorts.Add(tunnel.ListenPort))
            {
                errors.Add($"{prefix}.ListenPort: port {tunnel.ListenPort} is already used by another tunnel.");
            }

            switch (tunnel)
            {
                case DiscoverTunnelConfig discover:
                    if (discover.DiscoveryPort is < 1 or > 65535)
                    {
                        errors.Add($"{prefix}.DiscoveryPort: {discover.DiscoveryPort} is not a valid port (1-65535).");
                    }
                    break;

                case TunnelTunnelConfig tunnelMode:
                    if (!TryParseEndpoint(tunnelMode.ServerAddress, out _))
                    {
                        errors.Add($"{prefix}.ServerAddress: '{tunnelMode.ServerAddress}' is not a valid endpoint (expected host:port).");
                    }
                    break;

                case DirectTunnelConfig direct:
                    if (!TryParseEndpoint(direct.TargetAddress, out _))
                    {
                        errors.Add($"{prefix}.TargetAddress: '{direct.TargetAddress}' is not a valid endpoint (expected host:port).");
                    }
                    break;
            }
        }
    }

    private static void ValidateServer(ServerConfig? server, List<string> errors, List<string> warnings)
    {
        if (server == null)
            return;

        var prefix = "Server";

        if (server.ListenPort is < 1 or > 65535)
        {
            errors.Add($"{prefix}.ListenPort: {server.ListenPort} is not a valid port (1-65535).");
        }

        if (server.DiscoveryPort is < 1 or > 65535)
        {
            errors.Add($"{prefix}.DiscoveryPort: {server.DiscoveryPort} is not a valid port (1-65535).");
        }

        if (server.Services.Count == 0)
        {
            warnings.Add($"{prefix}.Services: no services configured — server will accept connections but match nothing.");
        }

        var serviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < server.Services.Count; i++)
        {
            var svc = server.Services[i];
            var svcPrefix = $"{prefix}.Services[{i}]";

            if (string.IsNullOrWhiteSpace(svc.Name))
            {
                errors.Add($"{svcPrefix}.Name: must not be empty.");
            }
            else if (!serviceNames.Add(svc.Name))
            {
                errors.Add($"{svcPrefix}.Name: duplicate service name '{svc.Name}'.");
            }

            if (!TryParseEndpoint(svc.TargetAddress, out _))
            {
                errors.Add($"{svcPrefix}.TargetAddress: '{svc.TargetAddress}' is not a valid endpoint (expected host:port).");
            }
        }
    }

    private static bool TryParseEndpoint(string? value, out IPEndPoint? endpoint)
    {
        endpoint = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return IPEndPoint.TryParse(value, out endpoint) || TryParseHostPort(value, out endpoint);
    }

    private static bool TryParseHostPort(string value, out IPEndPoint? endpoint)
    {
        endpoint = null;
        var colonIndex = value.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex == value.Length - 1)
            return false;

        var host = value[..colonIndex];
        var portStr = value[(colonIndex + 1)..];

        if (!int.TryParse(portStr, out var port) || port is < 1 or > 65535)
            return false;

        if (IPAddress.TryParse(host, out var address))
        {
            endpoint = new IPEndPoint(address, port);
            return true;
        }

        // DNS hostname — resolve it
        try
        {
            var addresses = Dns.GetHostAddresses(host);
            if (addresses.Length > 0)
            {
                endpoint = new IPEndPoint(addresses[0], port);
                return true;
            }
        }
        catch
        {
            // Resolution failed
        }

        return false;
    }
}
