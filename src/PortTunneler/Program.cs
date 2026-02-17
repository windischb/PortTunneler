using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PortTunneler.ServiceHelper;

namespace PortTunneler;

internal sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        var isService = args.Contains("--run-as-service");

        if (args.Contains("--install-service"))
        {
            return InstallService(GetArgValue(args, "--install-service"));
        }

        if (args.Contains("--uninstall-service"))
        {
            return UninstallService();
        }

        if (args.Contains("--start-service"))
        {
            return StartService();
        }

        if (args.Contains("--stop-service"))
        {
            return StopService();
        }

        if (args.Contains("--check-config"))
        {
            return CheckConfig();
        }

        if (args.Contains("--reload-config"))
        {
            return await ReloadConfig();
        }

        using var cts = new CancellationTokenSource();
        var firstCtrlCPressed = false;

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            if (!firstCtrlCPressed)
            {
                firstCtrlCPressed = true;
                eventArgs.Cancel = true;
                cts.Cancel();
                Console.WriteLine("Graceful shutdown initiated. Press Ctrl+C again to force shutdown.");
            }
            else
            {
                Environment.Exit(0);
            }
        };

        try
        {
            var config = LoadConfig();

            var serverEnabled = config.Server is { Enabled: true };
            var tunnelsEnabled = config.Tunnels.Enabled;

            var hostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(config.Logging.LogLevel);
                })
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton(config);
                    services.AddSingleton<DnsCache>();
                    services.AddSingleton<ProcessNonce>();
                    services.AddSingleton<DestinationMonitorRegistry>();

                    if (tunnelsEnabled)
                    {
                        services.AddSingleton<IClientConnectionFactory, ClientConnectionFactory>();
                        services.AddSingleton<ClientConnectionManager>();
                        services.AddHostedService(sp => sp.GetRequiredService<ClientConnectionManager>());
                    }

                    if (serverEnabled)
                    {
                        services.AddHostedService<ServiceDiscoveryHostedService>();
                        services.AddSingleton<ServerService>();
                        services.AddHostedService(sp => sp.GetRequiredService<ServerService>());
                    }

                    services.AddHostedService<CommandPipeService>();

                    if (isService)
                    {
                        services.AddHostedService<LifetimeService>();
                    }
                });

            if (isService)
            {
                if (OperatingSystem.IsWindows())
                {
                    hostBuilder.UseWindowsService();
                }
                else if (OperatingSystem.IsLinux())
                {
                    hostBuilder.UseSystemd();
                }
            }

            var host = hostBuilder.Build();

            // Validate config before starting
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            ConfigValidator.Validate(config, logger);
            LogStartupSummary(config);

            await host.RunAsync(cts.Token);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Host terminated unexpectedly: {ex}");
            return 1;
        }
    }

    private static PortTunnelerConfig LoadConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found at: {configPath}");
        }

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize(json, PortTunnelerJsonContext.Default.PortTunnelerConfig);

        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize config.json. The file may be empty or malformed.");
        }

        return config;
    }

    private static void LogStartupSummary(PortTunnelerConfig config)
    {
        Console.WriteLine($"Command pipe: {CommandPipeService.GetPipeName()}");

        if (!config.Tunnels.Enabled)
        {
            Console.WriteLine("Tunnels: disabled");
        }
        else if (config.Tunnels.Services.Count > 0)
        {
            Console.WriteLine($"Tunnels ({config.Tunnels.Services.Count}):");
            foreach (var tunnel in config.Tunnels.Services)
            {
                var mode = tunnel switch
                {
                    DiscoverTunnelConfig => "Discover",
                    TunnelTunnelConfig t => $"Tunnel -> {t.ServerAddress}",
                    DirectTunnelConfig d => $"Direct -> {d.TargetAddress}",
                    _ => "Unknown"
                };
                Console.WriteLine($"  {tunnel.Name} on port {tunnel.ListenPort} ({mode})");
            }
        }

        if (config.Server is { Enabled: false })
        {
            Console.WriteLine("Server: disabled");
        }
        else if (config.Server != null)
        {
            Console.WriteLine($"Server listening on port {config.Server.ListenPort}, discovery on port {config.Server.DiscoveryPort}");
            foreach (var svc in config.Server.Services)
            {
                Console.WriteLine($"  {svc.Name} -> {svc.TargetAddress}");
            }
        }

        Console.WriteLine("PortTunneler started.");
    }

    private static int InstallService(string? instanceName)
    {
        try
        {
            var serviceInstaller = ServiceInstallerFactory.Create();
            var serviceName = (instanceName != null ? $"porttunneler-{instanceName}" : "porttunneler").ToLowerInvariant();
            var displayName = instanceName != null ? $"PortTunneler - {instanceName}" : "PortTunneler";
            var fileName = Environment.ProcessPath
                           ?? throw new InvalidOperationException("Cannot determine executable path.");
            const string arguments = "--run-as-service";

            var serviceInfo = serviceInstaller.Install(serviceName, displayName, fileName, arguments);
            Console.WriteLine($"Service {serviceInfo.ServiceName} installed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to install service: {ex.Message}");
            return 1;
        }
    }

    private static int UninstallService()
    {
        try
        {
            var serviceInstaller = ServiceInstallerFactory.Create();
            var fileName = Environment.ProcessPath
                           ?? throw new InvalidOperationException("Cannot determine executable path.");

            var serviceInfo = serviceInstaller.GetServiceByExecutablePath(fileName);
            if (serviceInfo != null)
            {
                serviceInfo.Uninstall();
                Console.WriteLine($"Service {serviceInfo.ServiceName} uninstalled successfully.");
                return 0;
            }

            Console.Error.WriteLine("Service not found.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to uninstall service: {ex.Message}");
            return 1;
        }
    }

    private static int StartService()
    {
        try
        {
            var serviceInstaller = ServiceInstallerFactory.Create();
            var fileName = Environment.ProcessPath
                           ?? throw new InvalidOperationException("Cannot determine executable path.");

            var serviceInfo = serviceInstaller.GetServiceByExecutablePath(fileName);
            if (serviceInfo != null)
            {
                serviceInfo.Start();
                Console.WriteLine($"Service {serviceInfo.ServiceName} started successfully.");
                return 0;
            }

            Console.Error.WriteLine("Service not found.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start service: {ex.Message}");
            return 1;
        }
    }

    private static int CheckConfig()
    {
        try
        {
            var config = LoadConfig();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<Program>();
            ConfigValidator.Validate(config, logger);
            Console.WriteLine("Configuration is valid.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> ReloadConfig()
    {
        var pipeName = CommandPipeService.GetPipeName();
        try
        {
            await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await pipe.ConnectAsync(cts.Token);

            await using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);

            await writer.WriteLineAsync("reload".AsMemory(), cts.Token);
            var response = await reader.ReadLineAsync(cts.Token);

            // Read any remaining lines (multi-line responses)
            while (await reader.ReadLineAsync(cts.Token) is { } extra)
            {
                response += "\n" + extra;
            }

            Console.WriteLine(response ?? "No response from server.");
            return response != null && !response.StartsWith("Error", StringComparison.Ordinal) ? 0 : 1;
        }
        catch (TimeoutException)
        {
            Console.Error.WriteLine($"Timeout connecting to pipe '{pipeName}'. Is PortTunneler running?");
            return 1;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Could not connect to pipe '{pipeName}': {ex.Message}");
            return 1;
        }
    }

    private static int StopService()
    {
        try
        {
            var serviceInstaller = ServiceInstallerFactory.Create();
            var fileName = Environment.ProcessPath
                           ?? throw new InvalidOperationException("Cannot determine executable path.");

            var serviceInfo = serviceInstaller.GetServiceByExecutablePath(fileName);
            if (serviceInfo != null)
            {
                serviceInfo.Stop();
                Console.WriteLine($"Service {serviceInfo.ServiceName} stopped successfully.");
                return 0;
            }

            Console.Error.WriteLine("Service not found.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to stop service: {ex.Message}");
            return 1;
        }
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        var index = Array.IndexOf(args, flag);
        if (index < 0 || index + 1 >= args.Length)
            return null;

        var value = args[index + 1];
        return value.StartsWith("--") ? null : value;
    }
}
