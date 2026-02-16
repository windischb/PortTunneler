namespace PortTunneler;

public interface IClientConnectionFactory
{
    IClientConnection Create(TunnelConfig config);
}
