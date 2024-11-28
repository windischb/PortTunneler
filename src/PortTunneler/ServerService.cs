using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public class ServerService(ILogger<ServerService> logger, Config config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (config.Server == null)
            return;

        logger.LogInformation("Starting server...");

        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Bind(new IPEndPoint(IPAddress.Any, 51000));
        listener.Listen(100); // Maximum backlog of pending connections
        logger.LogInformation("Listening on port 51000 for client connections...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await listener.AcceptAsync(stoppingToken);
                logger.LogDebug("Accepted a client connection.");
                _ = HandleClientAsync(clientSocket, stoppingToken); // Process client in a separate task
            }
            catch (SocketException ex)
            {
                logger.LogError(ex, "SocketException while accepting client connection.");
            }
        }

        listener.Close();
    }

    private async Task HandleClientAsync(Socket clientSocket, CancellationToken stoppingToken)
    {
        using var clientStream = new NetworkStream(clientSocket, ownsSocket: true);

        try
        {
            while (clientSocket.Connected && !stoppingToken.IsCancellationRequested)
            {
                // Read the tag length
                var tagLengthBuffer = new byte[4];
                var bytesRead = await clientStream.ReadAsync(tagLengthBuffer, stoppingToken);
                if (bytesRead == 0)
                {
                    logger.LogInformation("Client disconnected.");
                    break;
                }

                var tagLength = BitConverter.ToInt32(tagLengthBuffer, 0);

                // Read the tag
                var tagBuffer = new byte[tagLength];
                bytesRead = await clientStream.ReadAsync(tagBuffer, stoppingToken);
                if (bytesRead == 0)
                {
                    logger.LogInformation("Client disconnected while reading tag.");
                    break;
                }

                var tag = Encoding.UTF8.GetString(tagBuffer);
                logger.LogInformation("Received tag: {Tag}", tag);

                if (tag == "ping")
                {
                    // Respond with "pong"
                    var pongBuffer = Encoding.UTF8.GetBytes("pong");
                    await clientStream.WriteAsync(pongBuffer, stoppingToken);
                    await clientStream.FlushAsync(stoppingToken);
                    continue; // Continue listening for further tags
                }

                // Lookup service configuration based on the tag
                var offeredService = config.Server!.OfferedServices.FirstOrDefault(s => s.ServiceName == tag);
                if (offeredService == null)
                {
                    logger.LogWarning("No matching service found for tag: {Tag}", tag);
                    continue;
                }

                // Handle the Direct type connection
                await HandleDirectConnectionAsync(clientStream, offeredService.Destination.ToIpEndpoint()!, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Operation canceled while handling client.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling client.");
        }
        finally
        {
            logger.LogDebug("Closing client connection.");
        }
    }

    private async Task HandleDirectConnectionAsync(NetworkStream clientStream, IPEndPoint destination, CancellationToken stoppingToken)
    {
        using var localSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await localSocket.ConnectAsync(destination, stoppingToken);
            using var localStream = new NetworkStream(localSocket, ownsSocket: true);

            var clientToServerTask = ForwardDataAsync(clientStream, localStream, "Client to Local", stoppingToken);
            var serverToClientTask = ForwardDataAsync(localStream, clientStream, "Local to Client", stoppingToken);

            await Task.WhenAll(clientToServerTask, serverToClientTask);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in HandleDirectConnectionAsync.");
        }
    }

    private async Task ForwardDataAsync(NetworkStream inputStream, NetworkStream outputStream, string direction, CancellationToken stoppingToken)
    {
        var buffer = new byte[4096];

        try
        {
            int bytesRead;
            while ((bytesRead = await inputStream.ReadAsync(buffer, stoppingToken)) > 0)
            {
                await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), stoppingToken);
                await outputStream.FlushAsync(stoppingToken);
                logger.LogDebug("{Direction}: Forwarded {BytesRead} bytes.", direction, bytesRead);
            }
        }
        catch (IOException ioEx)
        {
            logger.LogWarning(ioEx, "{Direction}: IO Exception (expected, often safe to ignore).", direction);
        }
        catch (ObjectDisposedException)
        {
            logger.LogDebug("{Direction}: Connection closed.", direction);
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("{Direction}: Operation canceled.", direction);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in {Direction}.", direction);
        }
        finally
        {
            logger.LogDebug("Closing streams in {Direction}.", direction);
        }
    }
}
