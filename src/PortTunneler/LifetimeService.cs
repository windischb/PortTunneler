using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PortTunneler;

sealed class LifetimeService : IHostedService
{
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<LifetimeService> _logger;

    public LifetimeService(IHostApplicationLifetime appLifetime, ILogger<LifetimeService> logger)
    {
        _appLifetime = appLifetime;
        _logger = logger;
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
        _logger.LogInformation("Application started.");
    }

    private void OnStopping()
    {
        _logger.LogInformation("Application stopping...");
    }

    private void OnStopped()
    {
        _logger.LogInformation("Application stopped.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Application is stopping...");
        return Task.CompletedTask;
    }
}
