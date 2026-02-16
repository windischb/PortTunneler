using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace PortTunneler.Connections;

public sealed class MultiplexingClientConnection : IClientConnection
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private readonly ILogger<MultiplexingClientConnection> _logger;
    private readonly string _serviceName;
    private readonly int _listenPort;
    private readonly IPEndPoint _serverEndpoint;
    private readonly CancellationTokenSource _cts = new();
    private Socket? _listener;

    public MultiplexingClientConnection(
        ILogger<MultiplexingClientConnection> logger,
        TunnelTunnelConfig tunnelConfig)
    {
        _logger = logger;
        _serviceName = tunnelConfig.Name;
        _listenPort = tunnelConfig.ListenPort;
        _serverEndpoint = IpEndpointExtensions.ParseEndpointOrThrow(tunnelConfig.ServerAddress, "ServerAddress");
    }

    internal MultiplexingClientConnection(
        ILogger<MultiplexingClientConnection> logger,
        string serviceName,
        int listenPort,
        IPEndPoint serverEndpoint)
    {
        _logger = logger;
        _serviceName = serviceName;
        _listenPort = listenPort;
        _serverEndpoint = serverEndpoint;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Multiplexing Client Connection is stopping.");
        await _cts.CancelAsync();
        _listener?.Close();
    }

    public void StartListening()
    {
        if (_listener != null) return;

        _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Bind(new IPEndPoint(IPAddress.Any, _listenPort));
        _listener.Listen(100);
        _logger.LogInformation("Listening on port {Port} for multiplexed connections to {Server} for service {ServiceName}...",
            _listenPort, _serverEndpoint, _serviceName);
        _ = AcceptClientsAsync().ContinueWith(
            t => _logger.LogCritical(t.Exception, "Unhandled exception in AcceptClientsAsync."),
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
                _logger.LogDebug("Accepted a connection on port {Port}. Service: {ServiceName}",
                    _listenPort, _serviceName);
                _ = HandleClientAsync(clientSocket, token).ContinueWith(
                    t => _logger.LogCritical(t.Exception, "Unhandled exception in HandleClientAsync."),
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
                _logger.LogError(ex, "SocketException in AcceptClientsAsync.");
            }
        }
    }

    private async Task HandleClientAsync(Socket clientSocket, CancellationToken ct)
    {
        using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(ConnectTimeout);
            await serverSocket.ConnectAsync(_serverEndpoint, connectCts.Token);

            await using var serverStream = new NetworkStream(serverSocket, ownsSocket: true);
            await using var clientStream = new NetworkStream(clientSocket, ownsSocket: true);

            if (!string.IsNullOrEmpty(_serviceName))
            {
                _logger.LogDebug("Sending service name {ServiceName} to the server.", _serviceName);
                await TunnelProtocol.WriteTagAsync(serverStream, _serviceName, ct);
            }

            var clientToServerTask = TcpForwarder.ForwardAsync(clientStream, serverStream, _logger, "Client to Server", ct);
            var serverToClientTask = TcpForwarder.ForwardAsync(serverStream, clientStream, _logger, "Server to Client", ct);

            await Task.WhenAll(clientToServerTask, serverToClientTask);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug("HandleClientAsync canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleClientAsync.");
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
