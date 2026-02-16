using System.Text.Json.Serialization;
using Serilog.Events;

namespace PortTunneler;

public sealed class PortTunnelerConfig
{
    public IReadOnlyList<TunnelConfig> Tunnels { get; init; } = [];
    public ServerConfig? Server { get; init; }
    public LoggingConfig Logging { get; init; } = new();
}

public sealed class LoggingConfig
{
    public LogEventLevel LogLevel { get; init; } = LogEventLevel.Warning;
}

[JsonConverter(typeof(TunnelConfigJsonConverter))]
public abstract class TunnelConfig
{
    public required string Name { get; init; }
    public required int ListenPort { get; init; }
}

public sealed class DiscoverTunnelConfig : TunnelConfig
{
    public int DiscoveryPort { get; init; } = 7608;
}

public sealed class TunnelTunnelConfig : TunnelConfig
{
    public required string ServerAddress { get; init; }
}

public sealed class DirectTunnelConfig : TunnelConfig
{
    public required string TargetAddress { get; init; }
}

public sealed class ServerConfig
{
    public int ListenPort { get; init; } = 51000;
    public int DiscoveryPort { get; init; } = 7608;
    public IReadOnlyList<ExposedServiceConfig> Services { get; init; } = [];
}

public sealed class ExposedServiceConfig
{
    public required string Name { get; init; }
    public required string TargetAddress { get; init; }
}
