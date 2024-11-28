using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

public class LifetimeService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<LifetimeService> _logger;
    private readonly Config _config;

    public LifetimeService(IHostApplicationLifetime appLifetime, ILogger<LifetimeService> logger,
        Config config)
    {
        _appLifetime = appLifetime;
        _logger = logger;
        _config = config;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _appLifetime.ApplicationStopping.Register(OnStopping);
        _appLifetime.ApplicationStopped.Register(OnStopped);
        _appLifetime.ApplicationStarted.Register(OnStarted);
         return Task.CompletedTask;
    }

    private void OnStarted()
    {
        Program._levelSwitch.MinimumLevel = _config.Logging.LogLevel;
    }

    private void OnStopping()
    {
        _logger.LogInformation("Application stopping...");
        // Add any additional cleanup or resource release here
    }

    private void OnStopped()
    {
        _logger.LogInformation("Application stopped.");
        // Add any additional cleanup or resource release here
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Application is stopping...");
        // Ensure the application stops gracefully
        Environment.Exit(0);
        return Task.CompletedTask;
    }
}