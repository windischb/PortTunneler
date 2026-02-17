using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PortTunneler.ServiceHelper;
using Serilog;
using Serilog.Core;

namespace PortTunneler;

internal class Program
{
    internal static readonly LoggingLevelSwitch LevelSwitch = new();

    public static async Task Main(string[] args)
    {
        var isService = args.Contains("--run-as-service");
        ConfigureLogging(isService);

        if (args.Contains("--install-service"))
        {
            InstallService();
            return;
        }

        if (args.Contains("--uninstall-service"))
        {
            UninstallService();
            return;
        }

        if (args.Contains("--start-service"))
        {
            StartService();
            return;
        }

        if (args.Contains("--stop-service"))
        {
            StopService();
            return;
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
                Log.CloseAndFlush();
                Environment.Exit(0);
            }
        };

        try
        {
            var config = LoadConfig();

            var hostBuilder = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((_, services) =>
                {
                    services.AddSingleton(config);
                    services.AddSingleton<DnsCache>();
                    services.AddSingleton<DestinationMonitorRegistry>();
                    services.AddSingleton<IClientConnectionFactory, ClientConnectionFactory>();
                    services.AddHostedService<ClientConnectionManager>();
                    services.AddHostedService<ServiceDiscoveryHostedService>();
                    services.AddHostedService<ServerService>();
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
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static PortTunnelerConfig LoadConfig()
    {
        var exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        if (string.IsNullOrEmpty(exePath))
        {
            exePath = AppContext.BaseDirectory;
        }

        var configPath = Path.Combine(exePath, "config.json");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file not found at: {configPath}");
        }

        var json = File.ReadAllText(configPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        // Handle default mode: tunnels without "Mode" field should become DiscoverTunnelConfig
        var config = JsonSerializer.Deserialize<PortTunnelerConfig>(json, options);

        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize config.json. The file may be empty or malformed.");
        }

        return config;
    }

    private static void LogStartupSummary(PortTunnelerConfig config, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (config.Tunnels.Count > 0)
        {
            logger.LogInformation("Configured {Count} tunnel(s):", config.Tunnels.Count);
            foreach (var tunnel in config.Tunnels)
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

        if (config.Server != null)
        {
            logger.LogInformation("Server listening on port {Port}, discovery on port {DiscoveryPort}",
                config.Server.ListenPort, config.Server.DiscoveryPort);
            foreach (var svc in config.Server.Services)
            {
                logger.LogInformation("  Offering {Name} -> {Target}", svc.Name, svc.TargetAddress);
            }
        }
    }

    private static void ConfigureLogging(bool isService)
    {
        LevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;

        var logConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch)
            .WriteTo.Console();

        if (isService)
        {
            logConfig = logConfig.WriteTo.File("logs/porttunneler-.txt", rollingInterval: RollingInterval.Day);
        }

        Log.Logger = logConfig.CreateLogger();
    }

    private static void InstallService()
    {
        var serviceInstaller = ServiceInstallerFactory.Create();
        const string serviceName = "PortTunneler";
        const string displayName = "PortTunneler Service";
        var fileName = Environment.ProcessPath
                       ?? throw new InvalidOperationException("Cannot determine executable path.");
        const string arguments = "--run-as-service";

        var serviceInfo = serviceInstaller.Install(serviceName, displayName, fileName, arguments);
        Console.WriteLine($"Service {serviceInfo.ServiceName} installed successfully.");
    }

    private static void UninstallService()
    {
        var serviceInstaller = ServiceInstallerFactory.Create();
        var fileName = Environment.ProcessPath
                       ?? throw new InvalidOperationException("Cannot determine executable path.");

        var serviceInfo = serviceInstaller.GetServiceByExecutablePath(fileName);
        if (serviceInfo != null)
        {
            serviceInfo.Uninstall();
            Console.WriteLine($"Service {serviceInfo.ServiceName} uninstalled successfully.");
        }
        else
        {
            Console.WriteLine("Service not found.");
        }
    }

    private static void StartService()
    {
        var serviceInstaller = ServiceInstallerFactory.Create();
        var fileName = Environment.ProcessPath
                       ?? throw new InvalidOperationException("Cannot determine executable path.");

        var serviceInfo = serviceInstaller.GetServiceByExecutablePath(fileName);
        if (serviceInfo != null)
        {
            serviceInfo.Start();
            Console.WriteLine($"Service {serviceInfo.ServiceName} started successfully.");
        }
        else
        {
            Console.WriteLine("Service not found.");
        }
    }

    private static void StopService()
    {
        var serviceInstaller = ServiceInstallerFactory.Create();
        var fileName = Environment.ProcessPath
                       ?? throw new InvalidOperationException("Cannot determine executable path.");

        var serviceInfo = serviceInstaller.GetServiceByExecutablePath(fileName);
        if (serviceInfo != null)
        {
            serviceInfo.Stop();
            Console.WriteLine($"Service {serviceInfo.ServiceName} stopped successfully.");
        }
        else
        {
            Console.WriteLine("Service not found.");
        }
    }
}
