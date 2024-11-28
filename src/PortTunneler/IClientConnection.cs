namespace PortTunneler;

public interface IClientConnection: IDisposable
{
    void StartListening();
    Task StopAsync(CancellationToken cancellationToken);

}