using Hgerman.ContentStudio.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Worker;

public sealed class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceProvider serviceProvider, ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ContentStudio Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var jobProcessor = scope.ServiceProvider.GetRequiredService<IJobProcessor>();

                var processed = await jobProcessor.ProcessNextPendingJobAsync(stoppingToken);

                if (processed)
                {
                    _logger.LogInformation("A queued job was processed.");
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                else
                {
                    _logger.LogDebug("No queued job found.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop failed.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("ContentStudio Worker stopped.");
    }
}