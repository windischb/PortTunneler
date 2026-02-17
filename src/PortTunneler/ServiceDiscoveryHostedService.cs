using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public class ServiceDiscoveryHostedService(ILogger<ServiceDiscoveryHostedService> logger, PortTunnelerConfig config)
    : BackgroundService
{
    private UdpClient? _udpClient;
    private HashSet<IPAddress>? _localIpAddresses;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (config.Server == null)
            return;

        var discoveryPort = config.Server.DiscoveryPort;
        var listenPort = config.Server.ListenPort;

        _localIpAddresses = GetLocalIpAddresses();
        _udpClient = new UdpClient(discoveryPort);
        logger.LogInformation("Listening for UDP discovery requests on port {Port}...", discoveryPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(stoppingToken);
                // Check if the message is from this machine
                if (_localIpAddresses!.Contains(result.RemoteEndPoint.Address))
                {
                    logger.LogDebug("Ignored broadcast message from self.");
                    continue;
                }

                var requestMessage = Encoding.UTF8.GetString(result.Buffer);

                logger.LogDebug("Received discovery request for service {ServiceName} from {RemoteEndPoint}.",
                    requestMessage, result.RemoteEndPoint);

                if (config.Server.Services.Any(s => s.WireTag == requestMessage))
                {
                    var responseMessage = listenPort.ToString();
                    var responseData = Encoding.UTF8.GetBytes(responseMessage);
                    await _udpClient.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);

                    logger.LogDebug("Responded with endpoint {EndPoint} for service {ServiceName}.",
                        responseMessage, requestMessage);
                }
                else
                {
                    logger.LogWarning("No matching service found for {ServiceName}.", requestMessage);
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

    private static HashSet<IPAddress> GetLocalIpAddresses()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        var addresses = new HashSet<IPAddress>(
            host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork));

        if (addresses.Count == 0)
            throw new InvalidOperationException("No network adapters with an IPv4 address in the system!");

        return addresses;
    }

    public override void Dispose()
    {
        _udpClient?.Dispose();
        base.Dispose();
    }
}
