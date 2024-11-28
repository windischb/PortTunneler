using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using PortTunneler.Connections;

namespace PortTunneler;

public class DestinationMonitor(IPEndPoint destination, ILogger<DestinationMonitor> logger)
{
    public readonly List<DiscoverClientConnection> RegisteredClients = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _isRunning;
    public Action<IPEndPoint>? NoClientsLeft;
    private Socket? _socket;

    public void RegisterClient(DiscoverClientConnection client)
    {
        lock (RegisteredClients)
        {
            if (!RegisteredClients.Contains(client))
            {
                RegisteredClients.Add(client);
            }
        }

        StartMonitoring();
    }

    public void UnregisterClient(DiscoverClientConnection client)
    {
        lock (RegisteredClients)
        {
            RegisteredClients.Remove(client);
            if (RegisteredClients.Count == 0)
            {
                StopMonitoring();
                NoClientsLeft?.Invoke(destination);
            }
        }
    }

    private void StartMonitoring()
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _ = MonitorDestinationAsync(_cts.Token);
    }

    private void StopMonitoring()
    {
        _isRunning = false;
        _cts.Cancel();
        _socket?.Close();
        _socket?.Dispose();
        _socket = null;
    }

    private async Task MonitorDestinationAsync(CancellationToken token)
    {
        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await _socket.ConnectAsync(destination, token);

            var networkStream = new NetworkStream(_socket, ownsSocket: false);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Send "ping" message
                    var serviceNameBuffer = "ping"u8.ToArray();
                    var serviceNameLengthBuffer = BitConverter.GetBytes(serviceNameBuffer.Length);
                    await networkStream.WriteAsync(serviceNameLengthBuffer, token);
                    await networkStream.WriteAsync(serviceNameBuffer, token);
                    await networkStream.FlushAsync(token);

                    // Read "pong" response
                    var buffer = new byte[4];
                    var bytesRead = await networkStream.ReadAsync(buffer, token);
                    if (bytesRead == 0)
                    {
                        logger.LogWarning("Connection to {Destination} was closed by the remote host.", destination);
                        await NotifyClients();
                        break;
                    }

                    var pongMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    if (pongMessage != "pong")
                    {
                        logger.LogWarning("Unexpected heartbeat response: {PongMessage}", pongMessage);
                        await NotifyClients();
                        break;
                    }

                    logger.LogDebug("Heartbeat successful: {Destination}", destination);

                    // Delay before the next heartbeat
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during heartbeat for {Destination}. Notifying clients...", destination);
                    await NotifyClients();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to {Destination}. Notifying clients...", destination);
            await NotifyClients();
        }
    }

    private Task NotifyClients()
    {
        lock (RegisteredClients)
        {
            foreach (var client in RegisteredClients.ToList())
            {
                client.NotifyDestinationUnreachable(destination).GetAwaiter().GetResult();
            }
        }

        return Task.CompletedTask;
    }
}
