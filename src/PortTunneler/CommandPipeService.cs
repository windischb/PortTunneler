using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public sealed class CommandPipeService(
    PortTunnelerConfig config,
    ILogger<CommandPipeService> logger,
    ClientConnectionManager? connectionManager = null,
    IClientConnectionFactory? connectionFactory = null,
    ServerService? serverService = null) : BackgroundService
{
    public static string GetPipeName()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        var canonical = Path.GetFullPath(configPath);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        var hex = Convert.ToHexStringLower(hash)[..12];
        return $"porttunneler-{hex}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeName = GetPipeName();
        logger.LogInformation("Command pipe listening on: {PipeName}", pipeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
                await using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                var command = await reader.ReadLineAsync(stoppingToken);
                if (command == null)
                    continue;

                var response = command.Trim().ToLowerInvariant() switch
                {
                    "reload" => await HandleReloadAsync(stoppingToken),
                    _ => $"Unknown command: {command.Trim()}"
                };

                await writer.WriteLineAsync(response.AsMemory(), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling pipe command.");
            }

            try
            {
                pipe.Disconnect();
            }
            catch
            {
                // Pipe may already be disconnected
            }
        }
    }

    private async Task<string> HandleReloadAsync(CancellationToken ct)
    {
        PortTunnelerConfig newConfig;
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
            var json = await File.ReadAllTextAsync(configPath, ct);
            newConfig = JsonSerializer.Deserialize(json, PortTunnelerJsonContext.Default.PortTunnelerConfig)
                        ?? throw new InvalidOperationException("Deserialized config was null.");
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            logger.LogError(ex, "Failed to read or parse config.json during reload.");
            return $"Error: {ex.Message}";
        }

        if (!ConfigValidator.TryValidate(newConfig, logger, out var validationError))
        {
            logger.LogError("Config validation failed during reload: {Error}", validationError);
            return $"Error: {validationError}";
        }

        var warnings = new List<string>();

        // Detect non-reloadable changes
        if (config.Tunnels.Enabled != newConfig.Tunnels.Enabled)
            warnings.Add("Tunnels.Enabled changed — requires restart to take effect.");

        if (config.Server != null && newConfig.Server != null)
        {
            if (config.Server.Enabled != newConfig.Server.Enabled)
                warnings.Add("Server.Enabled changed — requires restart to take effect.");
            if (config.Server.ListenPort != newConfig.Server.ListenPort)
                warnings.Add("Server.ListenPort changed — requires restart to take effect.");
            if (config.Server.DiscoveryPort != newConfig.Server.DiscoveryPort)
                warnings.Add("Server.DiscoveryPort changed — requires restart to take effect.");
        }
        else if (config.Server == null != (newConfig.Server == null))
        {
            warnings.Add("Server section added/removed — requires restart to take effect.");
        }

        // Apply tunnel diff
        if (connectionManager != null && connectionFactory != null && config.Tunnels.Enabled)
        {
            var diff = TunnelDiffer.Diff(config.Tunnels.Services, newConfig.Tunnels.Services);
            await connectionManager.ApplyDiffAsync(diff, ct);
            config.Tunnels.Services = newConfig.Tunnels.Services;
            config.Tunnels.DiscoveryPorts = newConfig.Tunnels.DiscoveryPorts;

            // Diff and apply server service changes
            if (config.Server != null && newConfig.Server != null)
            {
                ApplyServerDiff(config.Server.Services, newConfig.Server.Services);
                config.Server.Services = newConfig.Server.Services;
            }

            var summary = $"Reload complete: {diff.Added.Count} added, {diff.RemovedPorts.Count} removed, " +
                          $"{diff.Changed.Count} changed, {diff.UnchangedCount} unchanged.";

            if (warnings.Count > 0)
                summary += "\nWarnings:\n" + string.Join("\n", warnings.Select(w => $"  - {w}"));

            logger.LogInformation("{Summary}", summary);
            return summary;
        }

        // Even if tunnels are not enabled, diff and apply server service changes
        if (config.Server != null && newConfig.Server != null)
        {
            ApplyServerDiff(config.Server.Services, newConfig.Server.Services);
            config.Server.Services = newConfig.Server.Services;
        }

        var serverOnlySummary = "Reload complete: server services updated.";
        if (warnings.Count > 0)
            serverOnlySummary += "\nWarnings:\n" + string.Join("\n", warnings.Select(w => $"  - {w}"));

        logger.LogInformation("{Summary}", serverOnlySummary);
        return serverOnlySummary;
    }

    private void ApplyServerDiff(
        IReadOnlyList<ExposedServiceConfig> oldServices,
        IReadOnlyList<ExposedServiceConfig> newServices)
    {
        if (serverService == null)
            return;

        var diff = TunnelDiffer.DiffServer(oldServices, newServices);

        foreach (var wireTag in diff.RemovedWireTags)
        {
            logger.LogInformation("Server service {WireTag} removed, cancelling active connections.", wireTag);
            serverService.CancelConnectionsForService(wireTag);
        }

        foreach (var wireTag in diff.ChangedWireTags)
        {
            logger.LogInformation("Server service {WireTag} changed, cancelling active connections.", wireTag);
            serverService.CancelConnectionsForService(wireTag);
        }
    }
}
