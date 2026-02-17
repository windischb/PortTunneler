using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public sealed class ClientConnectionManager(
    IClientConnectionFactory connectionFactory,
    ILogger<ClientConnectionManager> logger,
    PortTunnelerConfig config) : IHostedService
{
    private ConcurrentDictionary<int, IClientConnection> Connections { get; } = new();

    public IClientConnection Add(TunnelConfig tunnelConfig)
    {
        var connection = connectionFactory.Create(tunnelConfig);

        if (!Connections.TryAdd(tunnelConfig.ListenPort, connection))
        {
            connection.Dispose();
            throw new InvalidOperationException($"A listener on port '{tunnelConfig.ListenPort}' already exists!");
        }

        return connection;
    }

    public async Task RemoveAsync(int localPort, CancellationToken cancellationToken = default)
    {
        if (Connections.TryRemove(localPort, out var clientService))
        {
            await clientService.StopAsync(cancellationToken);
            await clientService.DisposeAsync();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var tunnel in config.Tunnels.Services)
        {
            var connection = Add(tunnel);
            connection.StartListening();
            logger.LogInformation("Started tunnel {Name} on port {Port}.", tunnel.Name, tunnel.ListenPort);
        }
        return Task.CompletedTask;
    }

    public async Task ApplyDiffAsync(TunnelDiffer.DiffResult diff, CancellationToken ct)
    {
        foreach (var port in diff.RemovedPorts)
        {
            logger.LogInformation("Removing tunnel on port {Port}.", port);
            await RemoveAsync(port, ct);
        }

        foreach (var tunnel in diff.Changed)
        {
            logger.LogInformation("Restarting changed tunnel {Name} on port {Port}.", tunnel.Name, tunnel.ListenPort);
            await RemoveAsync(tunnel.ListenPort, ct);
        }

        foreach (var tunnel in diff.Added)
        {
            logger.LogInformation("Adding new tunnel {Name} on port {Port}.", tunnel.Name, tunnel.ListenPort);
            Add(tunnel).StartListening();
        }

        foreach (var tunnel in diff.Changed)
        {
            logger.LogInformation("Starting changed tunnel {Name} on port {Port}.", tunnel.Name, tunnel.ListenPort);
            Add(tunnel).StartListening();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var key in Connections.Keys)
        {
            await RemoveAsync(key, cancellationToken);
        }
    }
}
