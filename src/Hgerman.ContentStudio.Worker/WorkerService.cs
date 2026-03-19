using Hgerman.ContentStudio.Application.Interfaces;

namespace Hgerman.ContentStudio.Worker;

public class WorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkerService> _logger;

    public WorkerService(IServiceProvider serviceProvider, ILogger<WorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hgerman Content Studio worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IJobProcessor>();
                var processed = await processor.ProcessNextPendingJobAsync(stoppingToken);

                await Task.Delay(processed ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker loop failure.");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }
}
