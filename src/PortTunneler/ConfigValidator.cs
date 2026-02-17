using Microsoft.Extensions.Logging;

namespace PortTunneler;

public static class ConfigValidator
{
    public static void Validate(PortTunnelerConfig config, ILogger logger)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (config.Tunnels.Enabled)
        {
            ValidateTunnels(config.Tunnels, errors, warnings);
        }

        if (config.Server is { Enabled: true })
        {
            ValidateServer(config.Server, errors, warnings);
        }

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

    private static void ValidateTunnels(TunnelsConfig tunnels, List<string> errors, List<string> warnings)
    {
        for (var i = 0; i < tunnels.DiscoveryPorts.Count; i++)
        {
            var port = tunnels.DiscoveryPorts[i];
            if (port is < 1 or > 65535)
            {
                errors.Add($"Tunnels.DiscoveryPorts[{i}]: {port} is not a valid port (1-65535).");
            }
        }

        var usedPorts = new HashSet<int>();
        var usedWireTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < tunnels.Services.Count; i++)
        {
            var tunnel = tunnels.Services[i];
            var prefix = $"Tunnels.Services[{i}]";

            if (string.IsNullOrWhiteSpace(tunnel.Name))
            {
                errors.Add($"{prefix}.Name: must not be empty.");
            }

            if (tunnel.ServiceTag != null && string.IsNullOrWhiteSpace(tunnel.ServiceTag))
            {
                errors.Add($"{prefix}.ServiceTag: must not be empty when specified.");
            }

            if (!string.IsNullOrWhiteSpace(tunnel.WireTag) && !usedWireTags.Add(tunnel.WireTag))
            {
                errors.Add($"{prefix}.WireTag: duplicate wire tag '{tunnel.WireTag}'.");
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
                case TunnelTunnelConfig tunnelMode:
                    if (!TryParseEndpoint(tunnelMode.ServerAddress))
                    {
                        errors.Add($"{prefix}.ServerAddress: '{tunnelMode.ServerAddress}' is not a valid endpoint (expected host:port).");
                    }
                    break;

                case DirectTunnelConfig direct:
                    if (!TryParseEndpoint(direct.TargetAddress))
                    {
                        errors.Add($"{prefix}.TargetAddress: '{direct.TargetAddress}' is not a valid endpoint (expected host:port).");
                    }
                    break;
            }
        }
    }

    private static void ValidateServer(ServerConfig server, List<string> errors, List<string> warnings)
    {
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

        var serviceWireTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < server.Services.Count; i++)
        {
            var svc = server.Services[i];
            var svcPrefix = $"{prefix}.Services[{i}]";

            if (string.IsNullOrWhiteSpace(svc.Name))
            {
                errors.Add($"{svcPrefix}.Name: must not be empty.");
            }

            if (svc.ServiceTag != null && string.IsNullOrWhiteSpace(svc.ServiceTag))
            {
                errors.Add($"{svcPrefix}.ServiceTag: must not be empty when specified.");
            }

            if (!string.IsNullOrWhiteSpace(svc.WireTag) && !serviceWireTags.Add(svc.WireTag))
            {
                errors.Add($"{svcPrefix}.WireTag: duplicate service wire tag '{svc.WireTag}'.");
            }

            if (!TryParseEndpoint(svc.TargetAddress))
            {
                errors.Add($"{svcPrefix}.TargetAddress: '{svc.TargetAddress}' is not a valid endpoint (expected host:port).");
            }
        }
    }

    private static bool TryParseEndpoint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return DnsCache.TryParseHostPort(value, out _, out _);
    }
}
