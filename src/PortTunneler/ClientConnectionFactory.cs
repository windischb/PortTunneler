using Microsoft.Extensions.DependencyInjection;
using PortTunneler.Connections;

namespace PortTunneler;

public sealed class ClientConnectionFactory(IServiceProvider serviceProvider) : IClientConnectionFactory
{
    public IClientConnection Create(TunnelConfig config)
    {
        return config switch
        {
            DirectTunnelConfig direct => ActivatorUtilities.CreateInstance<DirectClientConnection>(
                serviceProvider, direct),
            TunnelTunnelConfig tunnel => ActivatorUtilities.CreateInstance<MultiplexingClientConnection>(
                serviceProvider, tunnel),
            DiscoverTunnelConfig discover => ActivatorUtilities.CreateInstance<DiscoverClientConnection>(
                serviceProvider, discover),
            _ => throw new InvalidOperationException($"Unknown tunnel config type: {config.GetType().Name}")
        };
    }
}
