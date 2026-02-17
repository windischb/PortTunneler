using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace PortTunneler.Connections;

public sealed class MultiplexingClientConnection : IClientConnection
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private readonly ILogger<MultiplexingClientConnection> _logger;
    private readonly string _serviceName;
    private readonly string _wireTag;
    private readonly int _listenPort;
    private readonly DnsCache _dnsCache;
    private readonly string _serverAddress;
    private readonly CancellationTokenSource _cts = new();
    private Socket? _listener;

    public MultiplexingClientConnection(
        ILogger<MultiplexingClientConnection> logger,
        DnsCache dnsCache,
        TunnelTunnelConfig tunnelConfig)
    {
        _logger = logger;
        _dnsCache = dnsCache;
        _serviceName = tunnelConfig.Name;
        _wireTag = tunnelConfig.WireTag;
        _listenPort = tunnelConfig.ListenPort;
        _serverAddress = tunnelConfig.ServerAddress;
    }

    internal MultiplexingClientConnection(
        ILogger<MultiplexingClientConnection> logger,
        string wireTag,
        int listenPort,
        IPEndPoint serverEndpoint)
    {
        _logger = logger;
        _dnsCache = new DnsCache();
        _serviceName = wireTag;
        _wireTag = wireTag;
        _listenPort = listenPort;
        _serverAddress = serverEndpoint.ToString();
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
            _listenPort, _serverAddress, _serviceName);
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
                clientSocket.NoDelay = true;
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
        try
        {
            var endpoint = await _dnsCache.ResolveAsync(_serverAddress, ct);
            using var serverSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.NoDelay = true;

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(ConnectTimeout);
            await serverSocket.ConnectAsync(endpoint, connectCts.Token);

            await using var serverStream = new NetworkStream(serverSocket, ownsSocket: true);
            await using var clientStream = new NetworkStream(clientSocket, ownsSocket: true);

            if (!string.IsNullOrEmpty(_wireTag))
            {
                _logger.LogDebug("Sending service tag {ServiceTag} to the server.", _wireTag);
                await TunnelProtocol.WriteTagAsync(serverStream, _wireTag, ct);
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
