using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortTunneler;

public sealed class TunnelConfigJsonConverter : JsonConverter<TunnelConfig>
{
    public override TunnelConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var mode = "Discover";
        if (root.TryGetProperty("Mode", out var modeProp))
        {
            mode = modeProp.GetString() ?? "Discover";
        }
        else if (root.TryGetProperty("mode", out modeProp))
        {
            mode = modeProp.GetString() ?? "Discover";
        }

        var rawJson = root.GetRawText();

        // Use a separate options instance without this converter to avoid infinite recursion
        var innerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        return mode.ToLowerInvariant() switch
        {
            "discover" => JsonSerializer.Deserialize<DiscoverTunnelConfig>(rawJson, innerOptions),
            "tunnel" => JsonSerializer.Deserialize<TunnelTunnelConfig>(rawJson, innerOptions),
            "direct" => JsonSerializer.Deserialize<DirectTunnelConfig>(rawJson, innerOptions),
            _ => throw new JsonException($"Unknown tunnel mode: '{mode}'. Expected 'Discover', 'Tunnel', or 'Direct'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, TunnelConfig value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("Name", value.Name);
        writer.WriteNumber("ListenPort", value.ListenPort);

        if (value.ServiceTag != null)
        {
            writer.WriteString("ServiceTag", value.ServiceTag);
        }

        switch (value)
        {
            case DiscoverTunnelConfig discover:
                writer.WriteNumber("DiscoveryPort", discover.DiscoveryPort);
                break;
            case TunnelTunnelConfig tunnel:
                writer.WriteString("Mode", "Tunnel");
                writer.WriteString("ServerAddress", tunnel.ServerAddress);
                break;
            case DirectTunnelConfig direct:
                writer.WriteString("Mode", "Direct");
                writer.WriteString("TargetAddress", direct.TargetAddress);
                break;
            default:
                throw new JsonException($"Unknown tunnel config type: {value.GetType().Name}");
        }

        writer.WriteEndObject();
    }
}
