using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PortTunneler
{
    public class DestinationMonitorRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<IPEndPoint, DestinationMonitor> _monitors = new();
        private readonly object _lock = new();

        public DestinationMonitorRegistry(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        
        public DestinationMonitor GetOrCreateMonitor(IPEndPoint destination)
        {
            lock (_lock)
            {
                if (!_monitors.TryGetValue(destination, out var monitor))
                {
                    var logger = _serviceProvider.GetRequiredService<ILogger<DestinationMonitor>>();
                    monitor = new DestinationMonitor(destination, logger);
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
                if (_monitors.ContainsKey(destination))
                {
                    _monitors[destination].NoClientsLeft = null;
                    _monitors.Remove(destination);
                }
            }
        }
    }
}