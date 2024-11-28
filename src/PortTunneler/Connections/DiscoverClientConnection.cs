using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PortTunneler.Connections;

public class DiscoverClientConnection(IServiceProvider serviceProvider, ConnectionInfo connectionInfo) : IClientConnection
{
    private readonly int _discoveryPort = 7608;
    private UdpClient? _udpClient;
    private bool _isDiscoveryActive;
    private readonly object _discoveryLock = new();
    private CancellationTokenSource _cts = new();
    private MultiplexingClientConnection? _tunnelClientConnection;
    private readonly ILogger<DiscoverClientConnection> _logger =
        serviceProvider.GetRequiredService<ILogger<DiscoverClientConnection>>();

    private DestinationMonitor? _destinationMonitor;
    private ConnectionInfo _connectionInfo = connectionInfo;

    private DestinationMonitorRegistry DestinationMonitorRegistry { get; } =
        serviceProvider.GetRequiredService<DestinationMonitorRegistry>();

    public void StartListening()
    {
        _ = DiscoverAndConnectServiceAsync(_cts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Direct Client Connection is stopping.");
        await _cts.CancelAsync();
        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;
        if (_tunnelClientConnection != null)
        {
            await _tunnelClientConnection.StopAsync(cancellationToken);
        }

    }

    public async Task NotifyDestinationUnreachable(IPEndPoint endPoint)
    {
        _destinationMonitor?.UnregisterClient(this);
        _destinationMonitor = null;
        await StopAsync(_cts.Token);
        _cts = new CancellationTokenSource();
        await Task.Delay(TimeSpan.FromSeconds(1));
        _ = DiscoverAndConnectServiceAsync(_cts.Token);

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

            while (!token.IsCancellationRequested)
            {
                var udpClient = new UdpClient(); // No specific port binding
                try
                {
                    _logger.LogDebug("Discovering {serviceName}...", _connectionInfo.ServiceName);

                    var requestData = Encoding.UTF8.GetBytes(_connectionInfo.ServiceName);
                    var broadcastEp = new IPEndPoint(IPAddress.Broadcast, _discoveryPort);

                    await udpClient.SendAsync(requestData, requestData.Length, broadcastEp);

                    var response = await ReceiveUdpResponseAsync(udpClient, token);
                    if (response != null)
                    {
                        _connectionInfo = _connectionInfo with { Destination = response, Direct = false };
                        _tunnelClientConnection = new MultiplexingClientConnection(serviceProvider, _connectionInfo);
                        _tunnelClientConnection.StartListening();

                        _destinationMonitor = DestinationMonitorRegistry.GetOrCreateMonitor(_connectionInfo.Destination);
                        _destinationMonitor.RegisterClient(this);

                        break; // Exit the discovery loop once a connection is established
                    }
                    else
                    {
                        _logger.LogDebug("No response for {serviceName}. Retrying...", _connectionInfo.ServiceName);
                        await Task.Delay(TimeSpan.FromSeconds(10), token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error discovering or connecting to {serviceName}. Retrying...",
                        _connectionInfo.ServiceName);
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                }
                finally
                {
                    udpClient.Dispose();
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

    private async Task<IPEndPoint?> ReceiveUdpResponseAsync(UdpClient udpClient, CancellationToken token)
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
        finally
        {
            udpClient.Dispose();
        }

    }

    public void Dispose()
    {
        _udpClient?.Dispose();
        _cts.Dispose();
        _tunnelClientConnection?.Dispose();
        _tunnelClientConnection = null;

    }
}