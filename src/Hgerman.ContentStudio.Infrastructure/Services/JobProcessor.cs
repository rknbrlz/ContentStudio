using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class JobProcessor : IJobProcessor
{
    private readonly ContentStudioDbContext _db;
    private readonly ILogger<JobProcessor> _logger;

    public JobProcessor(
        ContentStudioDbContext db,
        ILogger<JobProcessor> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> RecoverTimedOutJobsAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        var staleJobs = await _db.VideoJobs
            .Where(x => x.Status == Domain.Enums.VideoJobStatus.Processing
                     && x.LockExpiresAt != null
                     && x.LockExpiresAt < utcNow)
            .ToListAsync(cancellationToken);

        foreach (var job in staleJobs)
        {
            job.Status = Domain.Enums.VideoJobStatus.Queued;
            job.CurrentStep = Domain.Enums.VideoPipelineStep.Recovering;
            job.WorkerLockId = null;
            job.LockedBy = null;
            job.LockExpiresAt = null;
            job.LastHeartbeatAt = null;
            job.UpdatedDate = utcNow;
        }

        if (staleJobs.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return staleJobs.Count;
    }

    public async Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs
            .OrderBy(x => x.VideoJobId)
            .FirstOrDefaultAsync(x => x.Status == Domain.Enums.VideoJobStatus.Queued, cancellationToken);

        if (job is null)
            return false;

        job.Status = Domain.Enums.VideoJobStatus.Completed;
        job.CurrentStep = Domain.Enums.VideoPipelineStep.Completed;
        job.ProgressPercent = 100;
        job.CompletedDate = DateTime.UtcNow;
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job {VideoJobId} marked as completed by temporary processor.", job.VideoJobId);

        return true;
    }
}