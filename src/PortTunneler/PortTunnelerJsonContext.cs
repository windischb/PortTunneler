using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortTunneler;

[JsonSerializable(typeof(PortTunnelerConfig))]
[JsonSerializable(typeof(DiscoverTunnelConfig))]
[JsonSerializable(typeof(TunnelTunnelConfig))]
[JsonSerializable(typeof(DirectTunnelConfig))]
[JsonSerializable(typeof(ServerConfig))]
[JsonSerializable(typeof(ExposedServiceConfig))]
[JsonSerializable(typeof(LoggingConfig))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    Converters = [typeof(TunnelConfigJsonConverter)])]
public partial class PortTunnelerJsonContext : JsonSerializerContext;
