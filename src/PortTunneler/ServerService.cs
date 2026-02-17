using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public class ServerService(ILogger<ServerService> logger, DnsCache dnsCache, PortTunnelerConfig config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (config.Server == null)
            return;

        logger.LogInformation("Starting server...");

        var listenPort = config.Server.ListenPort;
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Bind(new IPEndPoint(IPAddress.Any, listenPort));
        listener.Listen(100);
        logger.LogInformation("Listening on port {Port} for client connections...", listenPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await listener.AcceptAsync(stoppingToken);
                logger.LogDebug("Accepted a client connection.");
                _ = HandleClientAsync(clientSocket, stoppingToken).ContinueWith(
                    t => logger.LogCritical(t.Exception, "Unhandled exception in HandleClientAsync."),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                logger.LogError(ex, "SocketException while accepting client connection.");
            }
        }
    }

    private async Task HandleClientAsync(Socket clientSocket, CancellationToken stoppingToken)
    {
        await using var clientStream = new NetworkStream(clientSocket, ownsSocket: true);

        try
        {
            while (clientSocket.Connected && !stoppingToken.IsCancellationRequested)
            {
                var tag = await TunnelProtocol.ReadTagAsync(clientStream, stoppingToken);
                if (tag == null)
                {
                    logger.LogDebug("Client disconnected.");
                    break;
                }

                logger.LogDebug("Received tag: {Tag}", tag);

                if (tag == "ping")
                {
                    await TunnelProtocol.WriteTagAsync(clientStream, "pong", stoppingToken);
                    continue;
                }

                var offeredService = config.Server!.Services.FirstOrDefault(s => s.WireTag == tag);
                if (offeredService == null)
                {
                    logger.LogWarning("No matching service found for tag: {Tag}", tag);
                    continue;
                }

                var targetEndpoint = await dnsCache.ResolveAsync(offeredService.TargetAddress, stoppingToken);
                await HandleDirectConnectionAsync(clientStream, targetEndpoint, stoppingToken);
            }
        }
        catch (InvalidDataException ex)
        {
            logger.LogWarning(ex, "Invalid data from client, closing connection.");
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
            await using var localStream = new NetworkStream(localSocket, ownsSocket: true);

            var clientToServerTask = TcpForwarder.ForwardAsync(clientStream, localStream, logger, "Client to Local", stoppingToken);
            var serverToClientTask = TcpForwarder.ForwardAsync(localStream, clientStream, logger, "Local to Client", stoppingToken);

            await Task.WhenAll(clientToServerTask, serverToClientTask);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in HandleDirectConnectionAsync.");
        }
    }
}
