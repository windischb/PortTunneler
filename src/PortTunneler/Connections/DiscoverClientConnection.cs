using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PortTunneler.Connections;

public sealed class DiscoverClientConnection : IClientConnection, IMonitorableClient
{
    private readonly int _discoveryPort;
    private readonly string _serviceName;
    private readonly string _wireTag;
    private readonly int _listenPort;
    private bool _isDiscoveryActive;
    private readonly Lock _discoveryLock = new();
    private CancellationTokenSource _cts = new();
    private MultiplexingClientConnection? _tunnelClientConnection;
    private readonly ILogger<DiscoverClientConnection> _logger;
    private readonly ILogger<MultiplexingClientConnection> _multiplexingLogger;
    private DestinationMonitor? _destinationMonitor;
    private readonly DestinationMonitorRegistry _monitorRegistry;

    public DiscoverClientConnection(
        ILogger<DiscoverClientConnection> logger,
        ILogger<MultiplexingClientConnection> multiplexingLogger,
        DestinationMonitorRegistry monitorRegistry,
        DiscoverTunnelConfig tunnelConfig)
    {
        _logger = logger;
        _multiplexingLogger = multiplexingLogger;
        _monitorRegistry = monitorRegistry;
        _serviceName = tunnelConfig.Name;
        _wireTag = tunnelConfig.WireTag;
        _listenPort = tunnelConfig.ListenPort;
        _discoveryPort = tunnelConfig.DiscoveryPort;
    }

    public void StartListening()
    {
        _ = DiscoverAndConnectServiceAsync(_cts.Token).ContinueWith(
            t => _logger.LogCritical(t.Exception, "Unhandled exception in DiscoverAndConnectServiceAsync."),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Discover Client Connection is stopping.");
        await _cts.CancelAsync();
        if (_tunnelClientConnection != null)
        {
            await _tunnelClientConnection.StopAsync(cancellationToken);
        }
    }

    public async Task NotifyDestinationUnreachable(IPEndPoint endPoint)
    {
        _destinationMonitor?.UnregisterClient(this);
        _destinationMonitor = null;
        if (_tunnelClientConnection != null)
        {
            await _tunnelClientConnection.DisposeAsync();
            _tunnelClientConnection = null;
        }
        await StopAsync(_cts.Token);
        var oldCts = _cts;
        _cts = new CancellationTokenSource();
        oldCts.Dispose();
        await Task.Delay(TimeSpan.FromSeconds(1));
        _ = DiscoverAndConnectServiceAsync(_cts.Token).ContinueWith(
            t => _logger.LogCritical(t.Exception, "Unhandled exception in DiscoverAndConnectServiceAsync."),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task DiscoverAndConnectServiceAsync(CancellationToken token)
    {
        lock (_discoveryLock)
        {
            if (_isDiscoveryActive) return;
            _isDiscoveryActive = true;
        }

        try
        {
            using var udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("Discovering {ServiceName}...", _serviceName);

                    var requestData = Encoding.UTF8.GetBytes(_wireTag);
                    var broadcastEp = new IPEndPoint(IPAddress.Broadcast, _discoveryPort);

                    await udpClient.SendAsync(requestData, requestData.Length, broadcastEp);

                    var response = await ReceiveUdpResponseAsync(udpClient, token);
                    if (response != null)
                    {
                        _tunnelClientConnection = new MultiplexingClientConnection(
                            _multiplexingLogger, _wireTag, _listenPort, response);
                        _tunnelClientConnection.StartListening();

                        _destinationMonitor = _monitorRegistry.GetOrCreateMonitor(response);
                        _destinationMonitor.RegisterClient(this);

                        break;
                    }

                    _logger.LogDebug("No response for {ServiceName}. Retrying...", _serviceName);
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error discovering or connecting to {ServiceName}. Retrying...",
                        _serviceName);
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                }
            }
        }
        finally
        {
            lock (_discoveryLock)
            {
                _isDiscoveryActive = false;
            }
        }
    }

    private static async Task<IPEndPoint?> ReceiveUdpResponseAsync(UdpClient udpClient, CancellationToken token)
    {
        try
        {
            var (isCompleted, result) =
                await udpClient.ReceiveAsync(token).AsTask().WithTimeout(TimeSpan.FromSeconds(5), token);
            if (isCompleted)
            {
                var port = Encoding.UTF8.GetString(result.Buffer);
                if (int.TryParse(port, out var p))
                {
                    return new IPEndPoint(result.RemoteEndPoint.Address, p);
                }
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_tunnelClientConnection != null)
        {
            await _tunnelClientConnection.DisposeAsync();
        }
        _tunnelClientConnection = null;
        _cts.Dispose();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _tunnelClientConnection?.Dispose();
        _tunnelClientConnection = null;
        _cts.Dispose();
    }
}
