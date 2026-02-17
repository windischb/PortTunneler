# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PortTunneler is a .NET 10.0 console application that tunnels TCP connections with multiplexing and UDP-based service discovery. It acts as both server and client: the server accepts multiplexed TCP connections and forwards them to destinations, while clients discover services via UDP broadcast and connect with automatic reconnection on failure.

## Build & Run Commands

Requires .NET SDK 10.x.

```bash
dotnet build src/PortTunneler.sln -c Release
dotnet run --project src/PortTunneler/PortTunneler.csproj
dotnet run --project src/PortTunneler/PortTunneler.csproj -- --run-as-service
dotnet publish src/PortTunneler/PortTunneler.csproj -c Release -o out/
dotnet format src/PortTunneler.sln
```

Service management flags: `--install-service`, `--uninstall-service`, `--start-service`, `--stop-service`

Tests use xUnit v3 (MTP runner) and NSubstitute. Package versions are centralized in `Directory.Packages.props`.
```bash
dotnet test src/PortTunneler.sln -c Release
dotnet run --project tests/PortTunneler.Tests.Unit/PortTunneler.Tests.Unit.csproj
dotnet run --project tests/PortTunneler.Tests.Unit/PortTunneler.Tests.Unit.csproj -- --filter "Name~MethodName"
```
Note: `DOTNET_ROOT` must be set if dotnet is not in `/usr/share/dotnet` for `dotnet test` to work with MTP.

## Architecture

**Entry point**: `Program.cs` — configures Serilog logging, DI container, and registers hosted services. Handles CLI flags for service install/control. Loads `config.json` from the executable directory.

**Server side**:
- `ServiceDiscoveryHostedService` — UDP listener on port 7608; responds to service name broadcasts with the TCP port
- `ServerService` — TCP listener on port 51000; reads a 4-byte LE length prefix + UTF-8 service name, then forwards bidirectionally to the configured destination

**Client side** (`ClientConnectionManager` creates the appropriate type based on config):
- `DirectClientConnection` — straight TCP forwarding to a known destination (no multiplexing)
- `MultiplexingClientConnection` — connects to server, sends service name header, then forwards
- `DiscoverClientConnection` — UDP broadcast discovery, then delegates to `MultiplexingClientConnection`; re-discovers on connection loss

**Health monitoring**: `DestinationMonitor` sends "ping" heartbeats every 5s over the multiplexing protocol; `DestinationMonitorRegistry` manages one monitor per endpoint and triggers rediscovery on failure.

**Configuration**: `config.json` shape defined in `Config.cs` — `Client.NeededServices` (LocalPort, ServiceName, optional Destination/Direct) and `Server.OfferedServices` (ServiceName, Destination, ConnectionType).

**Service helpers**: `ServiceHelper/` contains OS-specific service installers (Windows via PInvoke is implemented; Linux/macOS are stubs).

## Wire Protocol

- **UDP discovery** (port 7608): client broadcasts UTF-8 service name, server responds with UTF-8 port number
- **TCP multiplexing header**: 4-byte little-endian length prefix + UTF-8 service name bytes
- **Heartbeat**: "ping"/"pong" as length-prefixed tags (same framing as service name headers)

If you change ports or wire format, update both server and all client connection types.

## Code Style

- File-scoped namespaces, 4-space indentation, one type per file
- `Nullable` and `ImplicitUsings` enabled — treat nullability warnings as bugs
- Mark implementation types `sealed`; use records for immutable data
- Use `ILogger<T>` structured templates (`{Port}`) not interpolated strings
- Flow `CancellationToken` through async APIs; avoid blocking waits
- Fire-and-forget (`_ = Task...`) must handle exceptions inside the task
- Stream reads can be partial; 0 bytes means remote closed
