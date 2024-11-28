using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public class ServiceDiscoveryHostedService(ILogger<ServiceDiscoveryHostedService> logger, Config config)
    : BackgroundService
{
    private UdpClient _udpClient = new(7608);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (config.Server == null)
        {
            return;
        }
        logger.LogInformation("Listening for UDP discovery requests on port 7608...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(stoppingToken);
                // Check if the message is from this machine
                if (result.RemoteEndPoint.Address.Equals(GetLocalIpAddress()))
                {
                    logger.LogDebug("Ignored broadcast message from self.");
                    return;
                }
                
                var requestMessage = Encoding.UTF8.GetString(result.Buffer);

                logger.LogDebug("Received discovery request for service {ServiceName} from {RemoteEndPoint}.", requestMessage, result.RemoteEndPoint);

                if (config.Server.OfferedServices.Select(s => s.ServiceName).Contains(requestMessage))
                {
                    const string responseMessage = "51000";
                    var responseData = Encoding.UTF8.GetBytes(responseMessage);
                    await _udpClient.SendAsync(responseData, responseData.Length, result.RemoteEndPoint);

                    logger.LogDebug("Responded with endpoint {EndPoint} for service {ServiceName}.", responseMessage, requestMessage);
                }
                else
                {
                    logger.LogWarning("No matching service found for {ServiceName}.", requestMessage);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing discovery requests.");
            }
        }
    }

    private IPAddress GetLocalIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip;
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }
    
    public override void Dispose()
    {
        _udpClient.Dispose();
        base.Dispose();
    }
}