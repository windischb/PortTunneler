using Microsoft.Extensions.Logging;

namespace PortTunneler;

public sealed class PortTunnelerConfig
{
    public TunnelsConfig Tunnels { get; set; } = new();
    public ServerConfig? Server { get; set; }
    public LoggingConfig Logging { get; set; } = new();
}

public sealed class LoggingConfig
{
    public LogLevel LogLevel { get; set; } = LogLevel.Warning;
}

public sealed class TunnelsConfig
{
    public bool Enabled { get; set; } = true;
    public IReadOnlyList<int> DiscoveryPorts { get; set; } = [7608];
    public IReadOnlyList<TunnelConfig> Services { get; set; } = [];
}

public abstract class TunnelConfig
{
    public required string Name { get; set; }
    public required int ListenPort { get; set; }
    public string? ServiceTag { get; set; }
    public string WireTag => ServiceTag ?? Name;
}

public sealed class DiscoverTunnelConfig : TunnelConfig;

public sealed class TunnelTunnelConfig : TunnelConfig
{
    public required string ServerAddress { get; set; }
}

public sealed class DirectTunnelConfig : TunnelConfig
{
    public required string TargetAddress { get; set; }
}

public sealed class ServerConfig
{
    public bool Enabled { get; set; } = true;
    public int ListenPort { get; set; } = 51000;
    public int DiscoveryPort { get; set; } = 7608;
    public IReadOnlyList<ExposedServiceConfig> Services { get; set; } = [];
}

public sealed class ExposedServiceConfig
{
    public required string Name { get; set; }
    public required string TargetAddress { get; set; }
    public string? ServiceTag { get; set; }
    public string WireTag => ServiceTag ?? Name;
}
