using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public class ServiceDiscoveryHostedService(
    ILogger<ServiceDiscoveryHostedService> logger,
    PortTunnelerConfig config,
    ProcessNonce processNonce)
    : BackgroundService
{
    private UdpClient? _udpClient;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (config.Server == null)
            return;

        var discoveryPort = config.Server.DiscoveryPort;
        var listenPort = config.Server.ListenPort;

        _udpClient = new UdpClient(discoveryPort);
        logger.LogInformation("Listening for UDP discovery requests on port {Port}...", discoveryPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(stoppingToken);
                var message = Encoding.UTF8.GetString(result.Buffer);

                string wireTag;
                var newlineIndex = message.IndexOf('\n');
                if (newlineIndex >= 0)
                {
                    var nonce = message[..newlineIndex];
                    if (nonce == processNonce.Value)
                    {
                        logger.LogDebug("Ignored discovery request from self (nonce match).");
                        continue;
                    }
                    wireTag = message[(newlineIndex + 1)..];
                }
                else
                {
                    wireTag = message;
                }

                logger.LogDebug("Received discovery request for service {ServiceName} from {RemoteEndPoint}.",
                    wireTag, result.RemoteEndPoint);

                if (config.Server.Services.Any(s => s.WireTag == wireTag))
                {
                    var responseMessage = listenPort.ToString();
                    var responseData = Encoding.UTF8.GetBytes(responseMessage);
                    await _udpClient.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);

                    logger.LogDebug("Responded with endpoint {EndPoint} for service {ServiceName}.",
                        responseMessage, wireTag);
                }
                else
                {
                    logger.LogWarning("No matching service found for {ServiceName}.", wireTag);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing discovery requests.");
            }
        }
    }

    public override void Dispose()
    {
        _udpClient?.Dispose();
        base.Dispose();
    }
}
