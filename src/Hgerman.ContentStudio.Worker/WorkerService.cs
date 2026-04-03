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
        _logger.LogInformation(
            "Worker settings: IdleDelaySec={IdleDelaySec}, BusyDelaySec={BusyDelaySec}, ErrorDelaySec={ErrorDelaySec}",
            _options.IdleDelaySec,
            _options.BusyDelaySec,
            _options.ErrorDelaySec);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Worker loop tick started.");

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

                _logger.LogInformation("Checking pending jobs...");
                var anyProcessed = false;

                for (int i = 0; i < 5; i++)
                {
                    var processed = await processor.ProcessNextPendingJobAsync(stoppingToken);
                    if (!processed)
                    {
                        break;
                    }

                    anyProcessed = true;
                }

                _logger.LogInformation("Any pending job processed in this loop: {Processed}", anyProcessed);

                var autoPublishedCount = await automationService.PublishCompletedAutoJobsAsync(stoppingToken);
                if (autoPublishedCount > 0)
                {
                    _logger.LogInformation("Published {AutoPublishedCount} completed automated jobs.", autoPublishedCount);
                }

                var delay = anyProcessed
                    ? TimeSpan.FromSeconds(_options.BusyDelaySec)
                    : TimeSpan.FromSeconds(_options.IdleDelaySec);

                _logger.LogInformation("Worker loop completed. Waiting {DelaySeconds} sec.", delay.TotalSeconds);

                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker cancellation requested. Stopping gracefully.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop failure.");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.ErrorDelaySec), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Worker cancellation requested during error delay. Stopping gracefully.");
                    break;
                }
            }
        }

        _logger.LogInformation("Hgerman Content Studio V2 worker stopped.");
    }
}