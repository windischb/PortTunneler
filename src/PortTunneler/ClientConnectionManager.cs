using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PortTunneler.Connections;

namespace PortTunneler;

public class ClientConnectionManager : IHostedService
{
    
    private readonly ILogger<ClientConnectionManager> _logger;
    private readonly Config _clientConfig;
    private readonly IServiceProvider _serviceProvider;

    public ClientConnectionManager(IServiceProvider serviceProvider,
        ILogger<ClientConnectionManager> logger,
        Config configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _clientConfig = configuration;
    }

    private ConcurrentDictionary<int, IClientConnection> Connections { get; } = new();


    public IClientConnection Add(ConnectionInfo connectionInfo)
    {
        if (Connections.ContainsKey(connectionInfo.LocalPort))
        {
            throw new Exception($"A Listerner on port '{connectionInfo.LocalPort}' already exists!");
        }

        if (connectionInfo.Destination == null)
        {
            return Connections.GetOrAdd(connectionInfo.LocalPort,
                ActivatorUtilities.CreateInstance<DiscoverClientConnection>(_serviceProvider, connectionInfo));
        }

        if (!connectionInfo.Direct)
        {
            return Connections.GetOrAdd(connectionInfo.LocalPort,
                ActivatorUtilities.CreateInstance<MultiplexingClientConnection>(_serviceProvider, connectionInfo));
        }

        return Connections.GetOrAdd(connectionInfo.LocalPort,
            ActivatorUtilities.CreateInstance<DirectClientConnection>(_serviceProvider, connectionInfo));

    }

    public void Remove(int localPort)
    {
        if (Connections.TryRemove(localPort, out var clientService))
        {
            clientService.StopAsync(default);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var connectionInfo in _clientConfig.Client.NeededServices.Select(ns => new ConnectionInfo(ns.LocalPort, ns.ServiceName, ns.Destination.ToIpEndpoint(), ns.Direct)))
        {
            var connection = Add(connectionInfo);
            connection.StartListening();
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var connectionsKey in Connections.Keys)
        {
            Remove(connectionsKey);
        }
        
        return Task.CompletedTask;
    }
}