# PortTunneler Agent Notes
This repo is a small .NET app (net9.0) that tunnels TCP connections and supports UDP-based service discovery.

## Repo Layout
- `src/PortTunneler.sln` - Solution
- `src/PortTunneler/PortTunneler.csproj` - Main app project
- `src/PortTunneler/config.json` - Runtime config (copied to output)
- `src/PortTunneler/Connections/*.cs` - Client connection implementations

## Build / Run / Publish
Prereq: .NET SDK 9.x (project targets `net9.0`).

```bash
# Restore / clean (optional)
dotnet restore src/PortTunneler.sln
dotnet clean src/PortTunneler.sln -c Release

# Build
dotnet build src/PortTunneler.sln -c Release

# Run (loads config.json from built output directory)
dotnet run --project src/PortTunneler/PortTunneler.csproj --

# Run as service (enables LifetimeService and file logging)
dotnet run --project src/PortTunneler/PortTunneler.csproj -- --run-as-service

# Service control flags (handled in src/PortTunneler/Program.cs)
dotnet run --project src/PortTunneler/PortTunneler.csproj -- --install-service
dotnet run --project src/PortTunneler/PortTunneler.csproj -- --uninstall-service
dotnet run --project src/PortTunneler/PortTunneler.csproj -- --start-service
dotnet run --project src/PortTunneler/PortTunneler.csproj -- --stop-service

# Publish (framework-dependent by default)
dotnet publish src/PortTunneler/PortTunneler.csproj -c Release -o out/
```

## Ports / Protocol (current implementation)
- UDP discovery: server listens on port `7608` (`ServiceDiscoveryHostedService`); clients broadcast the service name.
- UDP discovery response: server responds with the TCP port as UTF-8 text (currently `"51000"`).
- TCP tunnel server: listens on port `51000` (`ServerService`).
- Multiplexing tag: client writes a 4-byte little-endian length prefix, then UTF-8 bytes of the service name.
- Heartbeat: tag `"ping"` gets response `"pong"` (used by `DestinationMonitor`).
- TCP data forwarding: bidirectional stream copy with fixed-size buffers.

If you change any of the ports/wire format, update both ends (server + all client connection types).

## Lint / Format
No repo-pinned linter/analyzer config (no `.editorconfig`, no StyleCop rules, no CI workflow).

```bash
# If available
dotnet format src/PortTunneler.sln
```

## Tests
There are currently no test projects in this repository.

```bash
# If/when tests are added
dotnet test src/PortTunneler.sln -c Release

# Single project
dotnet test path/to/Project.Tests.csproj -c Release

# Single test (filter examples)
dotnet test path/to/Project.Tests.csproj -c Release --filter "FullyQualifiedName~Namespace.TypeName.MethodName"
dotnet test path/to/Project.Tests.csproj -c Release --filter "Name~MethodName"
dotnet test path/to/Project.Tests.csproj -c Release --filter "Category=Fast"
```

When adding tests:
- Prefer xUnit (`dotnet new xunit`) unless the repo already standardizes on something else.
- Keep tests deterministic; avoid real network access unless explicitly an integration test.
- If you introduce integration tests, document any required ports/processes in this file.

## Debugging / Logging
- Logging uses Serilog via `.UseSerilog()` in `src/PortTunneler/Program.cs`.
- Default minimum level is set in code; service-mode can also write to a rolling file under `logs/`.
- `Config.Logging.LogLevel` is applied at startup by `src/PortTunneler/LifetimeService.cs` (service mode).
- Prefer adding temporary diagnostics as `LogDebug` and removing them before finishing a change.

## Service Helper Notes
- Service install/start/stop is implemented under `src/PortTunneler/ServiceHelper/` with OS-specific code.
- Windows code uses native APIs/PInvoke; keep changes minimal and test on the target OS.
- Command-line flags are the public interface; avoid renaming them without updating docs and scripts.

## Code Style (C#)
Project-wide settings in `src/PortTunneler/PortTunneler.csproj`:
- `Nullable` enabled: treat nullability warnings as real bugs.
- `ImplicitUsings` enabled: don’t add redundant `using` directives.

Formatting (match existing files):
- 4-space indentation; no tabs.
- Prefer file-scoped namespaces (`namespace PortTunneler;`) used by most files.
- Prefer one type per file; keep classes focused.
- Braces on new lines for blocks; allow short guard clauses.
- Prefer modern C# features already used here (records, `with`, primary constructors) when they improve clarity.
- Mark implementation types `sealed` when they aren’t designed for inheritance (common for connection classes).

Imports / usings:
- Order: `System.*`, then third-party, then local namespaces.
- Remove unused usings; keep the set minimal.
- Use `using var` for disposables scoped to a method.

Types / nullability:
- Avoid `!` unless an invariant is truly guaranteed; prefer explicit checks.
- Prefer `Try...` patterns over parse exceptions.
- Prefer immutable config-like data (records are already used, e.g. `ConnectionInfo`).

Naming:
- Types/methods/properties: PascalCase.
- Locals/parameters: camelCase; private fields commonly use `_name`.
- Async methods end with `Async`.
- Avoid abbreviations except well-known ones (`cts`, `udp`, `ip`).

Logging:
- Use `ILogger<T>` structured templates (placeholders like `{Port}`), not interpolated strings.
- Levels: Debug (per-connection), Info (lifecycle), Warning (expected issues), Error (unexpected failures).

Error handling:
- Prefer specific exception types (`InvalidOperationException`, `ArgumentException`, etc.).
- Don’t swallow exceptions silently; either handle safely or log and fail fast.
- In background loops, contain per-iteration failures so the service stays alive.

Async / concurrency:
- Flow `CancellationToken` through async APIs.
- Avoid blocking waits (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`); prefer `await`.
- Fire-and-forget (`_ = Task...`) must handle/log exceptions inside the task.
- Use `lock` only for small critical sections.

Networking / streams:
- Stream reads can be partial; don’t assume `ReadAsync` returns the requested length.
- `0` bytes read means remote closed.
- Dispose sockets/streams; be explicit with `ownsSocket` when using `NetworkStream`.
- Use clear buffer sizes; avoid unnecessary `FlushAsync` in tight loops.
- Don’t allocate per-iteration in tight loops unless measured; prefer reusing buffers when possible.

## Config Conventions
- `config.json` is loaded from the executable directory (`Program.cs` builds a path next to the assembly).
- `src/PortTunneler/config.json` is copied to output (`CopyToOutputDirectory=PreserveNewest`).
- JSON shape is defined by `src/PortTunneler/Config.cs`.

## Cursor / Copilot Instructions
- No Cursor rules found (`.cursor/rules/` or `.cursorrules` not present).
- No Copilot instructions found (`.github/copilot-instructions.md` not present).
