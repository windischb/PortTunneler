using System.Net;

namespace PortTunneler;

public record ConnectionInfo(int LocalPort, string ServiceName, IPEndPoint? Destination = null, bool Direct = false) { }
