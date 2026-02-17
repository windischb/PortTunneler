# PortTunneler

**PortTunneler** is a lightweight TCP tunneling application with multiplexing and automatic service discovery. It forwards TCP connections between networks, letting clients discover services dynamically via UDP broadcast — no manual IP configuration needed.

When a service moves or restarts on a different host, clients automatically rediscover and reconnect.

## Features

- **Three connection modes** — automatic discovery, multiplexed tunneling to a known server, or direct TCP forwarding
- **UDP service discovery** — clients broadcast a service name, servers respond with their address
- **Health monitoring** — periodic heartbeat checks with automatic reconnection on failure
- **Native AOT binaries** — single-file executables for Linux, Windows, and macOS (x64 and ARM64)
- **Run as a system service** — built-in install/uninstall for systemd, Windows Services, and launchd

## Installation

### Pre-built binaries

Download the latest release from [GitHub Releases](../../releases). Binaries are available for:

| Platform | Binary |
|----------|--------|
| Linux x64 | `PortTunneler-linux-x64` |
| Linux ARM64 | `PortTunneler-linux-arm64` |
| Windows x64 | `PortTunneler-win-x64.exe` |
| Windows ARM64 | `PortTunneler-win-arm64.exe` |
| macOS ARM64 | `PortTunneler-macos-arm64` |

On Linux/macOS, make the binary executable after downloading:

```bash
chmod +x PortTunneler-linux-x64
```

### Build from source

Requires .NET SDK 10.x.

```bash
dotnet publish src/PortTunneler/PortTunneler.csproj -c Release -r linux-x64
```

Replace `linux-x64` with your target runtime identifier (`win-x64`, `osx-arm64`, etc.).

## Quick Start

PortTunneler reads its configuration from a `config.json` file in the same directory as the executable.

### Scenario: Forward a local port to a remote service

**Server** (on the machine that can reach the target service):

```json
{
  "Server": {
    "Services": [
      { "Name": "my-database", "TargetAddress": "db-server:5432" }
    ]
  }
}
```

**Client** (on the machine that needs access):

```json
{
  "Tunnels": [
    { "Name": "my-database", "ListenPort": 5432 }
  ]
}
```

Start PortTunneler on both machines. The client broadcasts a UDP discovery request for `my-database`, the server responds, and the client opens a local listener on port 5432. Any connection to `localhost:5432` on the client is forwarded through the server to `db-server:5432`.

## Configuration

The `config.json` file supports comments (`//`) and trailing commas. Property names are case-insensitive.

### Tunnels

Tunnels define local listeners that forward connections. Each tunnel has a **mode** that determines how it finds the destination.

#### Discover mode (default)

Broadcasts a UDP request to find the server automatically. Reconnects if the server becomes unreachable.

```json
{
  "Tunnels": [
    {
      "Name": "web-api",
      "ListenPort": 8080
    }
  ]
}
```

| Field | Required | Default | Description |
|-------|----------|---------|-------------|
| `Name` | Yes | — | Identifier for this tunnel |
| `ListenPort` | Yes | — | Local port to listen on (1–65535) |
| `Mode` | No | `"Discover"` | Connection mode |
| `ServiceTag` | No | Value of `Name` | Tag sent to the server to identify the service |
| `DiscoveryPort` | No | `7608` | UDP port used for discovery broadcasts |

#### Tunnel mode

Connects to a known server address using the multiplexing protocol. No UDP discovery.

```json
{
  "Tunnels": [
    {
      "Name": "crm",
      "ListenPort": 9000,
      "Mode": "Tunnel",
      "ServerAddress": "proxy.example.com:51000"
    }
  ]
}
```

| Field | Required | Default | Description |
|-------|----------|---------|-------------|
| `Name` | Yes | — | Identifier for this tunnel |
| `ListenPort` | Yes | — | Local port to listen on |
| `Mode` | Yes | — | Must be `"Tunnel"` |
| `ServerAddress` | Yes | — | Server address as `host:port` |
| `ServiceTag` | No | Value of `Name` | Tag sent to identify the service |

#### Direct mode

Simple TCP forwarding to a fixed destination. No multiplexing protocol, no discovery.

```json
{
  "Tunnels": [
    {
      "Name": "legacy-rdp",
      "ListenPort": 3389,
      "Mode": "Direct",
      "TargetAddress": "legacy-server:3389"
    }
  ]
}
```

| Field | Required | Default | Description |
|-------|----------|---------|-------------|
| `Name` | Yes | — | Identifier for this tunnel |
| `ListenPort` | Yes | — | Local port to listen on |
| `Mode` | Yes | — | Must be `"Direct"` |
| `TargetAddress` | Yes | — | Destination address as `host:port` |

### Server

The server section makes the instance accept incoming tunnel connections and respond to discovery broadcasts.

```json
{
  "Server": {
    "ListenPort": 51000,
    "DiscoveryPort": 7608,
    "Services": [
      { "Name": "web-api", "TargetAddress": "localhost:5000" },
      { "Name": "database", "TargetAddress": "db.internal:5432", "ServiceTag": "db" }
    ]
  }
}
```

| Field | Required | Default | Description |
|-------|----------|---------|-------------|
| `ListenPort` | No | `51000` | TCP port for multiplexed connections |
| `DiscoveryPort` | No | `7608` | UDP port for service discovery |
| `Services` | Yes | — | List of services to offer |
| `Services[].Name` | Yes | — | Service name |
| `Services[].TargetAddress` | Yes | — | Destination to forward to as `host:port` |
| `Services[].ServiceTag` | No | Value of `Name` | Tag clients use to request this service |

A single instance can act as both server and client simultaneously.

### Logging

```json
{
  "Logging": {
    "LogLevel": "Information"
  }
}
```

Log levels: `Trace`, `Debug`, `Information`, `Warning` (default), `Error`, `Critical`, `None`.

### Validation

Use `--check-config` to validate your configuration without starting the application:

```bash
./PortTunneler --check-config
```

Validation rules:
- All `ListenPort` values must be unique across tunnels
- Service tags (or names, if no tag is set) must be unique
- Address fields must be valid `host:port` format
- Ports must be in the range 1–65535

## Running as a System Service

PortTunneler can install itself as a system service on all supported platforms.

```bash
# Install and enable the service
./PortTunneler --install-service

# Manage the service
./PortTunneler --start-service
./PortTunneler --stop-service

# Remove the service
./PortTunneler --uninstall-service
```

| Platform | Service Manager | Service File |
|----------|----------------|--------------|
| Windows | Windows Service Control Manager | Registered as `PortTunneler` service |
| Linux | systemd | `/etc/systemd/system/PortTunneler.service` |
| macOS | launchd | `/Library/LaunchDaemons/PortTunneler.plist` |

The service runs with `--run-as-service` automatically and is configured to restart on failure. On Linux, the service runs as the user who installed it.

## CLI Reference

| Flag | Description |
|------|-------------|
| *(none)* | Start PortTunneler as a console application |
| `--run-as-service` | Run as a system service (used automatically by the service manager) |
| `--check-config` | Validate `config.json` and exit |
| `--install-service` | Install as a system service |
| `--uninstall-service` | Remove the system service |
| `--start-service` | Start the installed service |
| `--stop-service` | Stop the running service |

Press `Ctrl+C` once for graceful shutdown, twice to force exit.

## How It Works

### Service discovery

1. The **client** broadcasts the service name via UDP on port 7608
2. The **server** receives the broadcast, looks up the service, and responds with its TCP port
3. The client connects to the server's TCP port and sends the service name as a header
4. The server forwards the connection to the configured target
5. Bidirectional data forwarding begins

### Health monitoring

For Discover and Tunnel mode connections, PortTunneler sends a heartbeat ("ping") to the server every 5 seconds. If the server stops responding, the client:

1. Closes the failed connection
2. Waits briefly, then restarts discovery (Discover mode) or reconnects (Tunnel mode)
3. Resumes forwarding once the server is reachable again

Direct mode connections have no health monitoring — they are simple pass-through.

### Default ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 7608 | UDP | Service discovery |
| 51000 | TCP | Multiplexed tunnel connections |

Both are configurable. Client tunnel listen ports are fully user-defined.

## Examples

### Database access across networks

**Server** (in the network with the database):

```json
{
  "Server": {
    "Services": [
      { "Name": "postgres", "TargetAddress": "db.internal:5432" },
      { "Name": "sql-server", "TargetAddress": "sql.internal:1433" }
    ]
  }
}
```

**Client** (developer machine):

```json
{
  "Tunnels": [
    { "Name": "postgres", "ListenPort": 5432 },
    { "Name": "sql-server", "ListenPort": 1433 }
  ]
}
```

Connect your database tools to `localhost:5432` or `localhost:1433` as usual.

### Mixed mode setup

```json
{
  "Tunnels": [
    { "Name": "api", "ListenPort": 8080 },
    { "Name": "crm", "ListenPort": 9000, "Mode": "Tunnel", "ServerAddress": "10.0.1.50:51000" },
    { "Name": "printer", "ListenPort": 9100, "Mode": "Direct", "TargetAddress": "192.168.1.100:9100" }
  ]
}
```

- `api` — discovered automatically via UDP broadcast
- `crm` — connects to a known server, using the multiplexing protocol
- `printer` — direct TCP forwarding, no protocol overhead

### Server and client on the same machine

```json
{
  "Server": {
    "Services": [
      { "Name": "local-api", "TargetAddress": "localhost:5000" }
    ]
  },
  "Tunnels": [
    { "Name": "remote-db", "ListenPort": 5432 }
  ]
}
```

This instance serves `local-api` to other clients while also tunneling `remote-db` from another server.

## Protocol Support

- **TCP** — fully supported for all tunneling and multiplexing
- **UDP** — used for service discovery only; tunneling UDP traffic is not supported

All connections are IPv4. IPv6 is not currently supported.

## License

See [LICENSE](LICENSE) for details.
