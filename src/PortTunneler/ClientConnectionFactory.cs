using Microsoft.Extensions.DependencyInjection;
using PortTunneler.Connections;

namespace PortTunneler;

public sealed class ClientConnectionFactory(IServiceProvider serviceProvider, PortTunnelerConfig config)
    : IClientConnectionFactory
{
    public IClientConnection Create(TunnelConfig tunnelConfig)
    {
        return tunnelConfig switch
        {
            DirectTunnelConfig direct => ActivatorUtilities.CreateInstance<DirectClientConnection>(
                serviceProvider, direct),
            TunnelTunnelConfig tunnel => ActivatorUtilities.CreateInstance<MultiplexingClientConnection>(
                serviceProvider, tunnel),
            DiscoverTunnelConfig discover => ActivatorUtilities.CreateInstance<DiscoverClientConnection>(
                serviceProvider, discover, config.Tunnels.DiscoveryPorts),
            _ => throw new InvalidOperationException($"Unknown tunnel config type: {tunnelConfig.GetType().Name}")
        };
    }
}
