using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PortTunneler.Connections;

public sealed class DirectClientConnection(IServiceProvider serviceProvider, ConnectionInfo connectionInfo)
    : IClientConnection
{
    private readonly ILogger<DirectClientConnection> _logger =
        serviceProvider.GetRequiredService<ILogger<DirectClientConnection>>();

    private Socket? _listener;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Direct Client Connection is stopping.");
        _listener?.Close();
        return Task.CompletedTask;
    }

    public void StartListening()
    {
        if (_listener == null)
        {
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Bind(new IPEndPoint(IPAddress.Any, connectionInfo.LocalPort));
            _listener.Listen(100); // Maximum backlog of pending connections
            _logger.LogInformation("Listening on port {Port} for client connections for service {ServiceName}...",
                connectionInfo.LocalPort, connectionInfo.ServiceName);
            _ = AcceptClientsAsync(_listener);
        }
    }

    private async Task AcceptClientsAsync(Socket listener)
    {
        while (listener.IsBound)
        {
            try
            {
                var clientSocket = await listener.AcceptAsync();
                _logger.LogDebug("Accepted a connection on port {Port}. Service: {ServiceName}",
                    ((IPEndPoint)listener.LocalEndPoint).Port, connectionInfo.ServiceName);
                _ = HandleClientAsync(clientSocket); // Handle client in a separate task
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "SocketException in AcceptClientsAsync.");
            }
        }
    }

    private async Task HandleClientAsync(Socket clientSocket)
    {
        using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            // Connect to the destination
            await serverSocket.ConnectAsync(connectionInfo.Destination!);
            _logger.LogDebug("Client connected: {ClientEndpoint} -> {ServerEndpoint}",
                clientSocket.RemoteEndPoint, $"{connectionInfo.Destination.Address}:{connectionInfo.Destination.Port}");

            var clientStream = new NetworkStream(clientSocket, ownsSocket: false);
            var serverStream = new NetworkStream(serverSocket, ownsSocket: false);

            // Start forwarding data
            var clientToServerTask = ForwardDataAsync(clientStream, serverStream, "Client to Server");
            var serverToClientTask = ForwardDataAsync(serverStream, clientStream, "Server to Client");
            await Task.WhenAll(clientToServerTask, serverToClientTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleClientAsync.");
        }
        finally
        {
            _logger.LogInformation("Closing client and server connections.");
            clientSocket.Close();
            serverSocket.Close();
        }
    }

    private async Task ForwardDataAsync(NetworkStream inputStream, NetworkStream outputStream, string direction)
    {
        var buffer = new byte[8192];

        try
        {
            int bytesRead;
            while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await outputStream.WriteAsync(buffer, 0, bytesRead);
                await outputStream.FlushAsync();
                _logger.LogDebug("{Direction}: Forwarded {BytesRead} bytes.", direction, bytesRead);
            }
        }
        catch (IOException ioEx)
        {
            _logger.LogDebug(ioEx, "{Direction}: IO Exception (expected, often safe to ignore): {Message}",
                direction, ioEx.Message);
        }
        catch (ObjectDisposedException)
        {
            _logger.LogDebug("{Direction}: Connection closed.", direction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Direction}", direction);
        }
        finally
        {
            _logger.LogDebug("Closing streams in {Direction}.", direction);
        }
    }

    public void Dispose()
    {
        _listener?.Close();
        _listener?.Dispose();
        _listener = null;
    }
}
