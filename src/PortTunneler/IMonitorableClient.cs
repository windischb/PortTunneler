using System.Net;

namespace PortTunneler;

public interface IMonitorableClient
{
    Task NotifyDestinationUnreachable(IPEndPoint endpoint);
}
