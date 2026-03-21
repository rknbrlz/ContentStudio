using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Shared.Options;
using Microsoft.Extensions.Options;

namespace Hgerman.ContentStudio.Worker;

public sealed class WorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkerService> _logger;
    private readonly WorkerOptions _options;

    public WorkerService(
        IServiceProvider serviceProvider,
        ILogger<WorkerService> logger,
        IOptions<WorkerOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hgerman Content Studio V2 worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();

                var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
                var processor = scope.ServiceProvider.GetRequiredService<IJobProcessor>();

                var autoCreatedCount = await automationService.RunScheduledAutomationsAsync(stoppingToken);
                if (autoCreatedCount > 0)
                {
                    _logger.LogInformation("Created {AutoCreatedCount} automated video jobs.", autoCreatedCount);
                }

                var recoveredCount = await processor.RecoverTimedOutJobsAsync(stoppingToken);
                if (recoveredCount > 0)
                {
                    _logger.LogWarning("Recovered {RecoveredCount} timed out/stale jobs.", recoveredCount);
                }

                var processed = await processor.ProcessNextPendingJobAsync(stoppingToken);

                var autoPublishedCount = await automationService.PublishCompletedAutoJobsAsync(stoppingToken);
                if (autoPublishedCount > 0)
                {
                    _logger.LogInformation("Published {AutoPublishedCount} completed automated jobs.", autoPublishedCount);
                }

                await Task.Delay(
                    processed ? TimeSpan.FromSeconds(_options.BusyDelaySec)
                              : TimeSpan.FromSeconds(_options.IdleDelaySec),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop failure.");
                await Task.Delay(TimeSpan.FromSeconds(_options.ErrorDelaySec), stoppingToken);
            }
        }

        _logger.LogInformation("Hgerman Content Studio V2 worker stopped.");
    }
}