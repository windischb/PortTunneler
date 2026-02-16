using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace PortTunneler.Connections;

public sealed class DirectClientConnection(ILogger<DirectClientConnection> logger, DirectTunnelConfig tunnelConfig)
    : IClientConnection
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private readonly CancellationTokenSource _cts = new();
    private Socket? _listener;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Direct Client Connection is stopping.");
        await _cts.CancelAsync();
        _listener?.Close();
    }

    public void StartListening()
    {
        if (_listener != null) return;

        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Bind(new IPEndPoint(IPAddress.Any, tunnelConfig.ListenPort));
        _listener.Listen(100);
        logger.LogInformation("Listening on port {Port} for direct connections to {Target} for service {ServiceName}...",
            tunnelConfig.ListenPort, tunnelConfig.TargetAddress, tunnelConfig.Name);
        _ = AcceptClientsAsync().ContinueWith(
            t => logger.LogCritical(t.Exception, "Unhandled exception in AcceptClientsAsync."),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task AcceptClientsAsync()
    {
        var token = _cts.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _listener!.AcceptAsync(token);
                logger.LogDebug("Accepted a connection on port {Port}. Service: {ServiceName}",
                    tunnelConfig.ListenPort, tunnelConfig.Name);
                _ = HandleClientAsync(clientSocket, token).ContinueWith(
                    t => logger.LogCritical(t.Exception, "Unhandled exception in HandleClientAsync."),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                logger.LogError(ex, "SocketException in AcceptClientsAsync.");
            }
        }
    }

    private async Task HandleClientAsync(Socket clientSocket, CancellationToken ct)
    {
        var destination = IpEndpointExtensions.ParseEndpointOrThrow(tunnelConfig.TargetAddress, "TargetAddress");
        using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(ConnectTimeout);
            await serverSocket.ConnectAsync(destination, connectCts.Token);

            logger.LogDebug("Client connected: {ClientEndpoint} -> {ServerEndpoint}",
                clientSocket.RemoteEndPoint, destination);

            await using var clientStream = new NetworkStream(clientSocket, ownsSocket: true);
            await using var serverStream = new NetworkStream(serverSocket, ownsSocket: true);

            var clientToServerTask = TcpForwarder.ForwardAsync(clientStream, serverStream, logger, "Client to Server", ct);
            var serverToClientTask = TcpForwarder.ForwardAsync(serverStream, clientStream, logger, "Server to Client", ct);
            await Task.WhenAll(clientToServerTask, serverToClientTask);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("HandleClientAsync canceled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in HandleClientAsync.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener?.Close();
        _listener?.Dispose();
        _listener = null;
        _cts.Dispose();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener?.Close();
        _listener?.Dispose();
        _listener = null;
        _cts.Dispose();
    }
}
