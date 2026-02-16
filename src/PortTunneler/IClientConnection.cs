namespace PortTunneler;

public interface IClientConnection : IAsyncDisposable, IDisposable
{
    void StartListening();
    Task StopAsync(CancellationToken cancellationToken);
}
