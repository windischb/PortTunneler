using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PortTunneler.ServiceHelper;
using Serilog;
using Serilog.Core;

namespace PortTunneler;

internal class Program
{
    internal static LoggingLevelSwitch _levelSwitch = new LoggingLevelSwitch();
    
    public static async Task Main(string[] args)
    {
        var isService = args.Contains("--run-as-service");
        ConfigureLogging(isService);

        // Handle service control arguments
        if (args.Contains("--install-service"))
        {
            await InstallService(args);
            return;
        }

        if (args.Contains("--uninstall-service"))
        {
            await UninstallService();
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

        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            if (!firstCtrlCPressed)
            {
                firstCtrlCPressed = true;
                eventArgs.Cancel = true; // Prevent the process from terminating.
                cts.Cancel(); // Trigger graceful shutdown.
                Console.WriteLine("Graceful shutdown initiated. Press Ctrl+C again to force shutdown.");
            }
            else
            {
                Log.CloseAndFlush();
                Environment.Exit(0); // Forcefully terminate the process.
            }
        };
        try
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    var exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                        
                    if (string.IsNullOrEmpty(exePath))
                    {
                        exePath = AppContext.BaseDirectory;
                    }

                    var configPath = Path.Combine(exePath, "config.json");

                    config.AddJsonFile(configPath, optional: false, reloadOnChange: false);
                    config.AddCommandLine(args);
                })
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(context.Configuration.Get<Config>());
                    services.AddSingleton<DestinationMonitorRegistry>();
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

    private static void ConfigureLogging(bool isService)
    {
        _levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;

        var logConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.Console();


        if (isService)
        {
            logConfig = logConfig.WriteTo.File("logs/.txt", rollingInterval: RollingInterval.Day);
        }

        Log.Logger = logConfig.CreateLogger();
    }
    
   private static async Task InstallService(string[] args)
        {
            var serviceInstaller = ServiceInstallerFactory.Create();
            string serviceName = "PortTunneler";
            string displayName = "PortTunneler Service";
            string fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            string arguments = "--run-as-service";

            // Install the service
            var serviceInfo = serviceInstaller.Install(serviceName, displayName, fileName, arguments);
            Console.WriteLine($"Service {serviceInfo.ServiceName} installed successfully.");
        }

        private static async Task UninstallService()
        {
            var serviceInstaller = ServiceInstallerFactory.Create();
            string fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

            // Find the service by executable path
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
            string fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

            // Find the service by executable path
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
            string fileName = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

            // Find the service by executable path
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