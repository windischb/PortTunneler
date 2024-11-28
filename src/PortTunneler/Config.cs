using Serilog.Events;

namespace PortTunneler;

public class Config
{
    public ClientConfig Client { get; set; } = new();
    public ServerConfig? Server { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

public class LoggingConfig
{
    public LogEventLevel LogLevel { get; set; } = LogEventLevel.Warning;
}
public class ClientConfig
{
    public List<NeededServiceConfig> NeededServices { get; set; } = new();
}

public class ServerConfig
{
    public List<OfferedServiceConfig> OfferedServices { get; set; } = new();
}

public class NeededServiceConfig
{
    public int LocalPort { get; set; }
    public bool Direct { get; set; }
    public string? Destination { get; set; }
    public string ServiceName { get; set; }
}

public class OfferedServiceConfig
{
    public string ServiceName { get; set; }
    public string Destination { get; set; }
    public ConnectionType Type { get; set; } = ConnectionType.Direct;
}

public enum ConnectionType
{
    Direct,
    Forward,
    Discovery
}
