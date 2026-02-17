using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public class DestinationMonitor(IPEndPoint destination, ILogger<DestinationMonitor> logger)
{
    private readonly List<IMonitorableClient> _registeredClients = [];
    private readonly Lock _lock = new();
    private int _isRunning;
    private CancellationTokenSource? _cts;
    public Action<IPEndPoint>? NoClientsLeft;
    private Socket? _socket;

    public void RegisterClient(IMonitorableClient client)
    {
        lock (_lock)
        {
            if (!_registeredClients.Contains(client))
            {
                _registeredClients.Add(client);
            }
        }

        StartMonitoring();
    }

    public void UnregisterClient(IMonitorableClient client)
    {
        lock (_lock)
        {
            _registeredClients.Remove(client);
            if (_registeredClients.Count == 0)
            {
                StopMonitoring();
                NoClientsLeft?.Invoke(destination);
            }
        }
    }

    private void StartMonitoring()
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            return;

        _cts = new CancellationTokenSource();
        _ = MonitorDestinationAsync(_cts.Token).ContinueWith(
            t => logger.LogCritical(t.Exception, "Unhandled exception in MonitorDestinationAsync."),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private void StopMonitoring()
    {
        Interlocked.Exchange(ref _isRunning, 0);
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _socket?.Close();
        _socket?.Dispose();
        _socket = null;
    }

    private async Task MonitorDestinationAsync(CancellationToken token)
    {
        try
        {
            _socket = new Socket(destination.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.NoDelay = true;
            await _socket.ConnectAsync(destination, token);

            await using var networkStream = new NetworkStream(_socket, ownsSocket: false);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await TunnelProtocol.WriteTagAsync(networkStream, "ping", token);
                    await networkStream.FlushAsync(token);

                    var pongMessage = await TunnelProtocol.ReadTagAsync(networkStream, token);
                    if (pongMessage == null)
                    {
                        logger.LogWarning("Connection to {Destination} was closed by the remote host.", destination);
                        await NotifyClientsAsync();
                        break;
                    }

                    if (pongMessage != "pong")
                    {
                        logger.LogWarning("Unexpected heartbeat response: {PongMessage}", pongMessage);
                        await NotifyClientsAsync();
                        break;
                    }

                    logger.LogDebug("Heartbeat successful: {Destination}", destination);
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during heartbeat for {Destination}. Notifying clients...", destination);
                    await NotifyClientsAsync();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to {Destination}. Notifying clients...", destination);
            await NotifyClientsAsync();
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    private async Task NotifyClientsAsync()
    {
        List<IMonitorableClient> snapshot;
        lock (_lock)
        {
            snapshot = [.. _registeredClients];
        }

        foreach (var client in snapshot)
        {
            try
            {
                await client.NotifyDestinationUnreachable(destination);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error notifying client about unreachable destination {Destination}.", destination);
            }
        }
    }
}
