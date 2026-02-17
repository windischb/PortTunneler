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
            return InstallService();
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
                    services.AddSingleton<DestinationMonitorRegistry>();

                    if (tunnelsEnabled)
                    {
                        services.AddSingleton<IClientConnectionFactory, ClientConnectionFactory>();
                        services.AddHostedService<ClientConnectionManager>();
                    }

                    if (serverEnabled)
                    {
                        services.AddHostedService<ServiceDiscoveryHostedService>();
                        services.AddHostedService<ServerService>();
                    }

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
            LogStartupSummary(config, logger);

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

    private static void LogStartupSummary(PortTunnelerConfig config, ILogger logger)
    {
        if (!config.Tunnels.Enabled)
        {
            logger.LogInformation("Tunnels disabled.");
        }
        else if (config.Tunnels.Services.Count > 0)
        {
            logger.LogInformation("Configured {Count} tunnel(s):", config.Tunnels.Services.Count);
            foreach (var tunnel in config.Tunnels.Services)
            {
                var mode = tunnel switch
                {
                    DiscoverTunnelConfig => "Discover",
                    TunnelTunnelConfig t => $"Tunnel -> {t.ServerAddress}",
                    DirectTunnelConfig d => $"Direct -> {d.TargetAddress}",
                    _ => "Unknown"
                };
                logger.LogInformation("  {Name} on port {Port} ({Mode})", tunnel.Name, tunnel.ListenPort, mode);
            }
        }

        if (config.Server is { Enabled: false })
        {
            logger.LogInformation("Server disabled.");
        }
        else if (config.Server != null)
        {
            logger.LogInformation("Server listening on port {Port}, discovery on port {DiscoveryPort}",
                config.Server.ListenPort, config.Server.DiscoveryPort);
            foreach (var svc in config.Server.Services)
            {
                logger.LogInformation("  Offering {Name} -> {Target}", svc.Name, svc.TargetAddress);
            }
        }
    }

    private static int InstallService()
    {
        try
        {
            var serviceInstaller = ServiceInstallerFactory.Create();
            const string serviceName = "PortTunneler";
            const string displayName = "PortTunneler Service";
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
}
