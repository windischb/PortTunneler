# PortTunneler

**PortTunneler** is a versatile and lightweight application designed to facilitate seamless communication between services by tunneling and multiplexing traffic through configurable ports.  
It offers dynamic service discovery, making it an essential tool for bridging and routing network services efficiently.

---

## Features

- **Port Tunneling**: Forward traffic from one port to another, supporting both local and remote destinations.
- **Service Multiplexing**: Handle multiple services over a single port using service tags.
- **Service Discovery**: Clients can discover available services dynamically via UDP.
- **Heartbeat Monitoring**: Ensures active connections by pinging services regularly and notifying clients on failures.

### Current Protocol Support
- **TCP**: Fully supported for tunneling and multiplexing.
- **UDP**: Used only for discovery. Tunneling UDP traffic is not supported in the current implementation.

---

## Use Cases

1. **Cross-Network Service Bridging**: Connect services across different networks with minimal configuration.
2. **Simplified Client Configuration**: Let clients discover services dynamically without pre-configured ports.
3. **Multiplexing Services**: Share a single port for multiple services, reducing resource usage and port conflicts.
4. **SQL Server Tunneling**: Route SQL Server traffic (TDS) through your tunneling system seamlessly.

---
