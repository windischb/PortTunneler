using System.Net;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public sealed class DestinationMonitorRegistry(ILogger<DestinationMonitor> monitorLogger)
{
    private readonly Dictionary<IPEndPoint, DestinationMonitor> _monitors = [];
    private readonly Lock _lock = new();

    public DestinationMonitor GetOrCreateMonitor(IPEndPoint destination)
    {
        lock (_lock)
        {
            if (!_monitors.TryGetValue(destination, out var monitor))
            {
                monitor = new DestinationMonitor(destination, monitorLogger);
                monitor.NoClientsLeft = RemoveMonitor;
                _monitors[destination] = monitor;
            }

            return monitor;
        }
    }

    private void RemoveMonitor(IPEndPoint destination)
    {
        lock (_lock)
        {
            if (_monitors.Remove(destination, out var monitor))
            {
                monitor.NoClientsLeft = null;
            }
        }
    }
}
