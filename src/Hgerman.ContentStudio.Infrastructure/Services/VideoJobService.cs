using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Application.Services;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
using Hgerman.ContentStudio.Infrastructure.Data;
using Hgerman.ContentStudio.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class VideoJobService : IVideoJobService
{
    private readonly ContentStudioDbContext _db;

    public VideoJobService(ContentStudioDbContext db)
    {
        _db = db;
    }

    public async Task<int> CreateJobAsync(CreateVideoJobRequest request, CancellationToken cancellationToken = default)
    {
        var job = new VideoJob
        {
            ProjectId = request.ProjectId,
            JobNo = JobNumberGenerator.Create(),
            Title = request.Title,
            Topic = request.Topic,
            SourcePrompt = request.SourcePrompt,
            LanguageCode = request.LanguageCode,
            PlatformType = request.PlatformType,
            ToneType = request.ToneType,
            DurationTargetSec = request.DurationTargetSec,
            AspectRatio = request.AspectRatio,
            VoiceProvider = request.VoiceProvider,
            VoiceName = request.VoiceName,
            SubtitleEnabled = request.SubtitleEnabled,
            ThumbnailEnabled = request.ThumbnailEnabled,
            InputMode = request.InputMode,
            Status = VideoJobStatus.Queued,
            CurrentStep = VideoPipelineStep.Queued,
            ProgressPercent = 0,
            OverlayEnabled = true,
            MotionMode = "cinematic",
            RenderProfile = "cinematic"
        };

        _db.VideoJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        return job.VideoJobId;
    }

    public async Task<IReadOnlyList<VideoJobListItemDto>> GetJobListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.VideoJobs
            .OrderByDescending(x => x.VideoJobId)
            .Select(x => new VideoJobListItemDto
            {
                VideoJobId = x.VideoJobId,
                JobNo = x.JobNo,
                Title = x.Title,
                LanguageCode = x.LanguageCode,
                PlatformType = x.PlatformType.ToString(),
                Status = x.Status.ToString(),
                CurrentStep = x.CurrentStep.ToString(),
                CreatedDate = x.CreatedDate,
                UpdatedDate = x.UpdatedDate,
                IsPublished = x.IsPublished
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<VideoJob?> GetJobAsync(int videoJobId, CancellationToken cancellationToken = default)
    {
        return await _db.VideoJobs
            .Include(x => x.Project)
            .Include(x => x.Scenes)
            .Include(x => x.Assets)
            .Include(x => x.PublishTasks)
            .Include(x => x.ErrorLogs)
            .Include(x => x.PrimarySourceAsset)
            .FirstOrDefaultAsync(x => x.VideoJobId == videoJobId, cancellationToken);
    }

    public async Task QueueJobAsync(int videoJobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs
            .FirstOrDefaultAsync(x => x.VideoJobId == videoJobId, cancellationToken);

        if (job == null)
            throw new InvalidOperationException($"Video job not found. VideoJobId={videoJobId}");

        job.Status = VideoJobStatus.Queued;
        job.CurrentStep = VideoPipelineStep.Queued;
        job.ErrorMessage = null;
        job.ProgressPercent = 0;
        job.StartedDate = null;
        job.CompletedDate = null;
        job.LastAttemptDate = null;
        job.NextRetryDate = null;
        job.LockedBy = null;
        job.WorkerLockId = null;
        job.LockExpiresAt = null;
        job.LastHeartbeatAt = null;
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryJobAsync(int videoJobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs
            .FirstOrDefaultAsync(x => x.VideoJobId == videoJobId, cancellationToken);

        if (job == null)
            throw new InvalidOperationException($"Video job not found. VideoJobId={videoJobId}");

        job.RetryCount += 1;
        job.Status = VideoJobStatus.Queued;
        job.CurrentStep = VideoPipelineStep.Queued;
        job.ErrorMessage = null;
        job.ProgressPercent = 0;
        job.StartedDate = null;
        job.CompletedDate = null;
        job.LastAttemptDate = null;
        job.NextRetryDate = null;
        job.LockedBy = null;
        job.WorkerLockId = null;
        job.LockExpiresAt = null;
        job.LastHeartbeatAt = null;
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var jobs = _db.VideoJobs.AsQueryable();

        var totalJobs = await jobs.CountAsync(cancellationToken);
        var queuedJobs = await jobs.CountAsync(x => x.Status == VideoJobStatus.Queued, cancellationToken);
        var processingJobs = await jobs.CountAsync(x => x.Status == VideoJobStatus.Processing, cancellationToken);
        var completedJobs = await jobs.CountAsync(x => x.Status == VideoJobStatus.Completed, cancellationToken);
        var failedJobs = await jobs.CountAsync(x => x.Status == VideoJobStatus.Failed, cancellationToken);

        var dto = new DashboardSummaryDto();

        SetPropertyIfExists(dto, "TotalJobs", totalJobs);
        SetPropertyIfExists(dto, "QueuedJobs", queuedJobs);
        SetPropertyIfExists(dto, "ProcessingJobs", processingJobs);
        SetPropertyIfExists(dto, "CompletedJobs", completedJobs);
        SetPropertyIfExists(dto, "FailedJobs", failedJobs);

        return dto;
    }

    public async Task AttachUploadedSourceImageAsync(
        int videoJobId,
        string fileName,
        string contentType,
        byte[] fileBytes,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs
            .FirstOrDefaultAsync(x => x.VideoJobId == videoJobId, cancellationToken);

        if (job == null)
            throw new InvalidOperationException($"Video job not found. VideoJobId={videoJobId}");

        var uploadsRoot = Path.Combine(AppContext.BaseDirectory, "storage", "uploads", "source-images");
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(fileName);
        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, safeFileName);

        await File.WriteAllBytesAsync(fullPath, fileBytes, cancellationToken);

        var asset = new Asset
        {
            ProjectId = job.ProjectId,
            VideoJobId = job.VideoJobId,
            ProviderName = "local",
            FileName = fileName,
            BlobPath = fullPath,
            PublicUrl = null,
            MimeType = contentType
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        job.PrimarySourceAssetId = asset.AssetId;
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void SetPropertyIfExists<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName);
        if (property == null || !property.CanWrite)
            return;

        property.SetValue(target, value);
    }
}